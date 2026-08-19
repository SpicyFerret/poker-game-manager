export interface AccessTokens {
  accessToken: string;
  refreshToken: string;
}

export type PaymentHandleType = 'Pix' | 'Other';

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  paymentType: PaymentHandleType | null;
  paymentHandle: string | null;
}

export interface RegisterRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
  displayName?: string;
}

export interface UpdateProfileRequest {
  displayName: string;
  paymentType: PaymentHandleType | null;
  paymentHandle: string | null;
}
