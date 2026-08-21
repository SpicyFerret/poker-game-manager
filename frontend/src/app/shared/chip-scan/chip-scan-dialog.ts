import { Component, ElementRef, OnDestroy, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { ChipColour, chipColour } from '../chip-colours';
import { ChipCalibration } from './chip-calibration';
import {
  AnalysedStack,
  ColourCandidate,
  FrameAnalysis,
  FrameIssue,
  RgbaImage,
  analyseFrame,
  ratioFromKnownCount,
} from './chip-counter';

/** One row this scan can fill in — a chip set's denomination, or a table's stock. */
export interface ChipScanCandidate {
  /** Whatever the caller uses to tell this row apart — a denominationId, or a form-array index as a string. */
  key: string;
  faceValue: number;
  /** Without a colour there is nothing to match a detected stack against — the row is left for typing. */
  colourToken: string | null;
  /** What is already in the field, so an unrecognised stack does not get wiped back to blank. */
  existingQuantity?: number | null;
}

export interface ChipScanData {
  /** Shown in the heading, e.g. "a contagem" or "a maleta". */
  title: string;
  chips: readonly ChipScanCandidate[];
}

export interface ChipScanResult {
  key: string;
  quantity: number;
}

type Phase = 'idle' | 'live' | 'calibrating' | 'unsupported' | 'confirm';

/** How many consecutive agreeing frames it takes before the reading is trusted. */
const FRAMES_TO_LOCK = 6;
const FRAME_INTERVAL_MS = 150;
/** Frames are analysed at this width; full camera resolution buys nothing and costs frame rate. */
const ANALYSIS_WIDTH = 320;

/**
 * A live viewfinder that counts the stacks in front of it continuously and
 * says what is wrong until the reading is worth trusting, rather than taking
 * one photo and hoping.
 *
 * The difference matters for accuracy, not just comfort: a single frame has
 * no way to reject itself. Here every frame is checked against the one before
 * it, and a count is only offered once it has held steady — and once the two
 * independent estimates in `chip-counter` agree with each other. Guidance is
 * derived from those same signals, so what the person is told to fix is
 * literally what is blocking the reading.
 *
 * Nothing leaves the phone: frames are analysed in memory, never stored,
 * never uploaded. Only the confirmed numbers are returned.
 */
@Component({
  selector: 'app-chip-scan-dialog',
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './chip-scan-dialog.html',
  styleUrl: './chip-scan-dialog.scss',
})
export class ChipScanDialog implements OnDestroy {
  private readonly dialogRef = inject(MatDialogRef<ChipScanDialog>);
  private readonly calibration = inject(ChipCalibration);

  protected readonly data = inject<ChipScanData>(MAT_DIALOG_DATA);

  protected readonly phase = signal<Phase>('idle');
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly tilted = signal(false);

  /** The most recent frame's reading, for the live overlay. */
  protected readonly analysis = signal<FrameAnalysis | null>(null);
  /** Width of the analysed frame, so overlay badges can be placed against it. */
  private readonly frameWidth = signal(0);
  protected readonly steadyFrames = signal(0);
  protected readonly detectedCount = signal(0);

  /** What the person types during calibration: how many chips are really in the stack. */
  protected readonly calibrationCount = signal<number | null>(null);

  protected readonly quantities = signal<Record<string, number | null>>(
    Object.fromEntries(this.data.chips.map((chip) => [chip.key, chip.existingQuantity ?? null])),
  );

  protected readonly isCalibrated = this.calibration.isCalibrated;

  protected readonly progress = computed(() =>
    Math.min(100, Math.round((this.steadyFrames() / FRAMES_TO_LOCK) * 100)),
  );

  private readonly videoRef = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('captureCanvas');

  // A wide band rather than a narrow box: several stacks side by side need
  // to fit across it, not just one.
  protected readonly guide = { left: 0.04, top: 0.12, width: 0.92, height: 0.76 };

  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private lastSignature = '';

  private readonly onOrientation = (event: DeviceOrientationEvent): void => {
    this.tilted.set(event.gamma !== null && Math.abs(event.gamma) > 12);
  };

  ngOnDestroy(): void {
    this.stopCamera();
  }

  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }

  // ---------------------------------------------------------------- guidance

  /**
   * The single most useful thing to say right now. One message, not a list:
   * someone holding a phone over a table reads one line, and the first fix
   * often makes the rest go away anyway.
   */
  protected guidance(): string {
    if (this.tilted()) {
      return $localize`:@@chipScan.guideTilt:Incline o celular para ficar de frente para as pilhas.`;
    }

    const issues = this.analysis()?.issues ?? [];
    const has = (issue: FrameIssue): boolean => issues.includes(issue);

    if (has('too-dark')) {
      return $localize`:@@chipScan.guideDark:Está escuro demais — acenda uma luz ou chegue mais perto.`;
    }
    if (has('too-bright')) {
      return $localize`:@@chipScan.guideBright:Muita luz refletindo — tire o brilho direto das fichas.`;
    }
    if (has('no-stacks')) {
      return $localize`:@@chipScan.guideNoStacks:Coloque as pilhas de lado dentro da moldura, separadas por cor.`;
    }
    if (has('clipped')) {
      return $localize`:@@chipScan.guideClipped:A pilha está saindo da moldura — afaste um pouco o celular.`;
    }
    if (has('too-small')) {
      return $localize`:@@chipScan.guideTooSmall:As pilhas estão pequenas demais — chegue mais perto.`;
    }
    if (has('disagreement')) {
      return $localize`:@@chipScan.guideUnsure:Ainda não consigo ter certeza — segure firme e enquadre a pilha inteira.`;
    }

    if (this.steadyFrames() > 0) {
      return $localize`:@@chipScan.guideHold:Segure assim...`;
    }

    return $localize`:@@chipScan.guideAim:Aponte para as pilhas.`;
  }

  protected readingLabel(stack: AnalysedStack): string {
    return stack.quantity === null
      ? $localize`:@@chipScan.readingUnsure:?`
      : String(stack.quantity);
  }

  /**
   * Where to float a stack's badge, as a percentage across the guide box.
   * The analysed frame *is* the guide box, so a column in one maps straight
   * onto the other — no offset to undo.
   */
  protected readingPosition(stack: AnalysedStack): number {
    const width = this.frameWidth();

    if (width === 0) {
      return 50;
    }

    return (((stack.columns.start + stack.columns.end) / 2) / width) * 100;
  }

  // ------------------------------------------------------------------ camera

  protected async startCamera(next: 'live' | 'calibrating' = 'live'): Promise<void> {
    this.errorMessage.set(null);

    if (!navigator.mediaDevices?.getUserMedia) {
      this.phase.set('unsupported');
      return;
    }

    await this.requestOrientationPermission();

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1280 } },
        audio: false,
      });
    } catch {
      this.errorMessage.set(
        $localize`:@@chipScan.cameraDenied:Não foi possível abrir a câmera. Você pode digitar a contagem normalmente.`,
      );
      this.phase.set('unsupported');
      return;
    }

    this.steadyFrames.set(0);
    this.lastSignature = '';
    this.phase.set(next);
    window.addEventListener('deviceorientation', this.onOrientation);

    // The <video> element is always in the DOM (see the template), just
    // hidden until now — no render-timing race to wait out before this can
    // attach the stream to it.
    const video = this.videoRef()?.nativeElement;
    if (video) {
      video.srcObject = this.stream;
      await video.play().catch(() => undefined);
    }

    this.timer = setInterval(() => this.tick(), FRAME_INTERVAL_MS);
  }

  protected startCalibration(): void {
    void this.startCamera('calibrating');
  }

  private async requestOrientationPermission(): Promise<void> {
    const ctor = (
      window as unknown as {
        DeviceOrientationEvent?: { requestPermission?: () => Promise<'granted' | 'denied'> };
      }
    ).DeviceOrientationEvent;

    if (typeof ctor?.requestPermission === 'function') {
      try {
        await ctor.requestPermission();
      } catch {
        // No tilt hint without it; scanning still works.
      }
    }
  }

  private stopCamera(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }

    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    window.removeEventListener('deviceorientation', this.onOrientation);
  }

  // ------------------------------------------------------------- frame loop

  /**
   * One pass over the current frame. Counts as "steady" only when this
   * frame's whole reading — every stack's colour and count — is identical to
   * the last one's, so a number that is drifting can never reach the lock.
   */
  private tick(): void {
    const frame = this.grabGuideRegion();

    if (!frame) {
      return;
    }

    const result = analyseFrame(frame, this.colourCandidates(), this.calibration.ratio());
    this.frameWidth.set(frame.width);
    this.analysis.set(result);

    const usable =
      result.issues.length === 0 &&
      result.stacks.length > 0 &&
      result.stacks.every((s) => s.quantity !== null) &&
      !this.tilted();

    if (!usable) {
      this.steadyFrames.set(0);
      this.lastSignature = '';
      return;
    }

    const signature = result.stacks.map((s) => `${s.colourToken}:${s.quantity}`).join('|');

    if (signature === this.lastSignature) {
      this.steadyFrames.update((n) => n + 1);
    } else {
      this.lastSignature = signature;
      this.steadyFrames.set(1);
    }

    if (this.steadyFrames() >= FRAMES_TO_LOCK) {
      this.phase() === 'calibrating' ? this.lockCalibration(result) : this.lockReading(result);
    }
  }

  private lockReading(result: FrameAnalysis): void {
    this.stopCamera();
    this.applyDetections(result.stacks);
    this.phase.set('confirm');
  }

  /**
   * Calibration locks on the *shape* of a single stack, then waits for the
   * person to say how many chips are really in it. Until they do, there is
   * nothing to compute a ratio from.
   */
  private lockCalibration(result: FrameAnalysis): void {
    if (result.stacks.length !== 1) {
      // More than one stack in frame is ambiguous — which one are they
      // counting? Keep looking rather than guessing.
      this.steadyFrames.set(0);
      return;
    }

    this.stopCamera();
    this.analysis.set(result);
  }

  protected confirmCalibration(): void {
    const stack = this.analysis()?.stacks[0];
    const known = this.calibrationCount();

    if (!stack || known === null || known <= 0) {
      return;
    }

    const ratio = ratioFromKnownCount(
      stack.columns.end - stack.columns.start,
      stack.rows.end - stack.rows.start,
      known,
    );

    if (ratio === null) {
      this.errorMessage.set(
        $localize`:@@chipScan.calibrationFailed:Não deu para medir essa pilha. Tente de novo com a pilha inteira dentro da moldura.`,
      );
      return;
    }

    this.calibration.save(ratio);
    this.phase.set('idle');
    this.analysis.set(null);
    this.calibrationCount.set(null);
  }

  /** True once calibration has a locked frame and is waiting for the real count. */
  protected awaitingCalibrationCount(): boolean {
    return this.phase() === 'calibrating' && this.stream === null && this.analysis() !== null;
  }

  // ------------------------------------------------------------- detections

  /** Only the colours actually in play — matching against a handful of real candidates is far more reliable than the whole palette. */
  private colourCandidates(): ColourCandidate[] {
    const seen = new Set<string>();
    const candidates: ColourCandidate[] = [];

    for (const chip of this.data.chips) {
      const colour = chip.colourToken ? this.colourOf(chip.colourToken) : null;

      if (colour && !seen.has(colour.token)) {
        seen.add(colour.token);
        candidates.push({ token: colour.token, swatch: colour.swatch });
      }
    }

    return candidates;
  }

  /** First match wins per colour — several stacks of one colour is a mis-take, not a merge. */
  private applyDetections(detected: readonly AnalysedStack[]): void {
    this.detectedCount.set(detected.length);

    const byColour = new Map<string, number>();
    for (const stack of detected) {
      if (stack.colourToken && stack.quantity !== null && !byColour.has(stack.colourToken)) {
        byColour.set(stack.colourToken, stack.quantity);
      }
    }

    this.quantities.update((current) => {
      const next = { ...current };

      for (const chip of this.data.chips) {
        const found = chip.colourToken ? byColour.get(chip.colourToken) : undefined;
        if (found !== undefined) {
          next[chip.key] = found;
        }
      }

      return next;
    });
  }

  private grabGuideRegion(): RgbaImage | null {
    const video = this.videoRef()?.nativeElement;
    const canvas = this.canvasRef()?.nativeElement;

    if (!video || !canvas || !video.videoWidth || !video.videoHeight) {
      return null;
    }

    const sourceWidth = video.videoWidth * this.guide.width;
    const sourceHeight = video.videoHeight * this.guide.height;

    const targetWidth = Math.min(ANALYSIS_WIDTH, Math.floor(sourceWidth));
    const targetHeight = Math.max(1, Math.floor(sourceHeight * (targetWidth / sourceWidth)));

    canvas.width = targetWidth;
    canvas.height = targetHeight;

    const context = canvas.getContext('2d', { willReadFrequently: true });
    if (!context) {
      return null;
    }

    context.drawImage(
      video,
      Math.floor(video.videoWidth * this.guide.left),
      Math.floor(video.videoHeight * this.guide.top),
      Math.floor(sourceWidth),
      Math.floor(sourceHeight),
      0,
      0,
      targetWidth,
      targetHeight,
    );

    return context.getImageData(0, 0, targetWidth, targetHeight);
  }

  // ----------------------------------------------------------------- actions

  /** Back to the start rather than straight into the camera, so a bad take can be re-framed. */
  protected retry(): void {
    this.stopCamera();
    this.phase.set('idle');
    this.errorMessage.set(null);
    this.analysis.set(null);
    this.steadyFrames.set(0);
  }

  protected set(key: string, value: number | null): void {
    // Negative chips are not a thing, and the API refuses them anyway.
    const quantity = value === null || value < 0 ? null : Math.floor(value);

    this.quantities.update((current) => ({ ...current, [key]: quantity }));
  }

  protected setCalibrationCount(value: number | null): void {
    this.calibrationCount.set(value === null || value < 1 ? null : Math.floor(value));
  }

  protected confirm(): void {
    const quantities = this.quantities();

    this.dialogRef.close(
      this.data.chips.map((chip) => ({
        key: chip.key,
        quantity: quantities[chip.key] ?? 0,
      })) satisfies ChipScanResult[],
    );
  }
}
