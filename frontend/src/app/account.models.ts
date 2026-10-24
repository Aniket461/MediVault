import { Role } from './auth.models';

export interface AccountProfile {
  id: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  address: string | null;
  hospital: string | null;
  payPalEmail: string | null;
  role: Role;
  secondOpinionFee: number | null;
  self: boolean;
  spent: number;
  refunded: number;
  received: number;
}
