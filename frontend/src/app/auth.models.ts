export type Role = 'Patient' | 'Doctor' | 'Admin';
export type AccountStatus = 'Pending' | 'Approved' | 'Rejected';

export interface User {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  address: string;
  hospital: string | null;
  payPalEmail: string | null;
  role: Role;
  status: AccountStatus;
  secondOpinionFee: number | null;
}

export interface IdentityDocument {
  id: string;
  fileName: string;
}

export interface UserDetail extends User {
  documents: IdentityDocument[];
  spent: number;
  refunded: number;
  received: number;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface MessageResponse {
  message: string;
}

