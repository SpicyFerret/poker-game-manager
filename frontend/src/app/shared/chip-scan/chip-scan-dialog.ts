import { Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { CHIP_COLOURS, ChipColour, chipColour } from '../chip-colours';
import { scanChipStack } from './chip-counter';

export interface ChipScanData {
  /** Shown in the heading, e.g. "Ficha 25". */
  label: string;
}

export interface ChipScanResult {
  quantity: number;
  colourToken: string | null;
}

type Phase = 'idle' | 'live' | 'unsupported' | 'confirm';

/**
 * Guides the camera onto a side-on photo of one stack of chips, estimates how
 * many are in it (and which registered colour they are), and always hands
 * that estimate back through an editable confirmation step — never straight
 * into a form. Nothing leaves the phone: the photo is processed in memory and
 * discarded the moment the estimate is computed.
 *
 * Starting the camera waits for an explicit tap rather than firing on open,
 * for two reasons: a permission prompt should not ambush someone the instant
 * a dialog appears, and iOS only grants the motion-tilt hint in response to a
 * user gesture, so this same tap asks for both at once.
 */
@Component({
  selector: 'app-chip-scan-dialog',
  imports: [
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './chip-scan-dialog.html',
  styleUrl: './chip-scan-dialog.scss',
})
export class ChipScanDialog implements OnDestroy {
  private readonly dialogRef = inject(MatDialogRef<ChipScanDialog>);

  protected readonly data = inject<ChipScanData>(MAT_DIALOG_DATA);

  protected readonly phase = signal<Phase>('idle');
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly tiltWarning = signal(false);

  protected readonly quantity = signal<number | null>(null);
  protected readonly colourToken = signal<string | null>(null);

  protected readonly palette = CHIP_COLOURS;

  private readonly videoRef = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('captureCanvas');

  /** The guide box, as a fraction of the video frame — used for both the CSS overlay and the crop. */
  protected readonly guide = { left: 0.2, top: 0.15, width: 0.6, height: 0.7 };

  private stream: MediaStream | null = null;
  private readonly onOrientation = (event: DeviceOrientationEvent): void => {
    this.tiltWarning.set(event.gamma !== null && Math.abs(event.gamma) > 12);
  };

  ngOnDestroy(): void {
    this.stopCamera();
  }

  protected colourOf(token: string | null): ChipColour | null {
    return chipColour(token);
  }

  protected async startCamera(): Promise<void> {
    this.errorMessage.set(null);

    if (!navigator.mediaDevices?.getUserMedia) {
      this.phase.set('unsupported');
      return;
    }

    await this.requestOrientationPermission();

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
        audio: false,
      });
    } catch {
      this.errorMessage.set(
        $localize`:@@chipScan.cameraDenied:Não foi possível abrir a câmera. Você pode digitar a contagem normalmente.`,
      );
      this.phase.set('unsupported');
      return;
    }

    this.phase.set('live');
    window.addEventListener('deviceorientation', this.onOrientation);

    // The <video> element is always in the DOM (see the template), just
    // hidden until now — no render-timing race to wait out before this can
    // attach the stream to it.
    const video = this.videoRef()?.nativeElement;
    if (video) {
      video.srcObject = this.stream;
      await video.play().catch(() => undefined);
    }
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
        // No tilt hint without it; capture still works.
      }
    }
  }

  private stopCamera(): void {
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
    window.removeEventListener('deviceorientation', this.onOrientation);
  }

  protected capture(): void {
    const imageData = this.captureGuideRegion();
    this.stopCamera();

    if (imageData) {
      const estimate = scanChipStack(
        imageData,
        CHIP_COLOURS.map((c) => ({ token: c.token, swatch: c.swatch })),
      );
      this.quantity.set(estimate.quantity);
      this.colourToken.set(estimate.colourToken);
    } else {
      this.quantity.set(null);
      this.colourToken.set(null);
    }

    this.phase.set('confirm');
  }

  private captureGuideRegion(): ImageData | null {
    const video = this.videoRef()?.nativeElement;
    const canvas = this.canvasRef()?.nativeElement;

    if (!video || !canvas || !video.videoWidth || !video.videoHeight) {
      return null;
    }

    const vw = video.videoWidth;
    const vh = video.videoHeight;
    canvas.width = vw;
    canvas.height = vh;

    const context = canvas.getContext('2d');
    if (!context) {
      return null;
    }

    context.drawImage(video, 0, 0, vw, vh);

    const sx = Math.floor(vw * this.guide.left);
    const sy = Math.floor(vh * this.guide.top);
    const sw = Math.floor(vw * this.guide.width);
    const sh = Math.floor(vh * this.guide.height);

    return context.getImageData(sx, sy, sw, sh);
  }

  /** Back to the start rather than straight into the camera, so a bad take can be re-framed. */
  protected retry(): void {
    this.phase.set('idle');
    this.errorMessage.set(null);
  }

  protected setQuantity(value: number | null): void {
    this.quantity.set(value === null || value < 0 ? null : Math.floor(value));
  }

  protected confirm(): void {
    this.dialogRef.close({
      quantity: this.quantity() ?? 0,
      colourToken: this.colourToken(),
    } satisfies ChipScanResult);
  }
}
