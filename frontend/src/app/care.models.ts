export interface MedicalRecord {
  id: string;
  fileName: string;
  description: string;
  createdAt: string;
  locked: boolean;
  archived: boolean;
}

export interface DoctorOption {
  id: string;
  fullName: string;
  hospital: string | null;
  fee: number;
}

export type OpinionStatus = 'AwaitingPayment' | 'Open' | 'Answered' | 'Refunded';
export type RefundCause = 'Deadline' | 'Declined';

export interface OpinionFile {
  id: string;
  fileName: string;
  description: string;
}

export interface ClinicalIntake {
  symptoms: string[];
  feeling: string;
  duration: string;
  intensity: string;
  consulted: string;
  consultedNote: string;
}

export interface ClinicalAid {
  overview: string;
  reported: string;
  earlierCare: string;
  impression: string;
  reviewPoints: string[];
}

export interface OpinionRequestBody {
  doctorId: string;
  recordIds: string[];
  message: string;
  symptoms: string[];
  feeling: string;
  duration: string;
  intensity: string;
  consulted: string;
  consultedNote: string;
}

export interface PayPalConfig {
  enabled: boolean;
  clientId: string;
  currency: string;
}

export interface Opinion {
  id: string;
  doctorId: string;
  doctorName: string;
  hospital: string | null;
  patientId: string;
  patientName: string;
  fee: number;
  doctorAmount: number | null;
  platformAmount: number | null;
  status: OpinionStatus;
  refundCause: RefundCause | null;
  message: string;
  intake: ClinicalIntake | null;
  clinicalAid: ClinicalAid | null;
  opinion: string | null;
  createdAt: string;
  replyBy: string | null;
  files: OpinionFile[];
  notice: string | null;
}
