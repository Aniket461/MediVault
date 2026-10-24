# MediVault

MediVault is a personal library for medical records, and a way to ask a doctor for a paid second opinion on the files that matter.

Lab reports, prescriptions, scans, and discharge notes usually end up scattered across email, downloads, and paper. MediVault keeps that history in one place. When a patient wants another opinion, they share the records already in the library instead of collecting and sending them again.

## What it does

A patient uploads reports, scans, prescriptions, and other files. The library stays private. Asking a doctor does not open the whole history. The doctor sees only the files attached to that request.

The doctor sets the fee. MediVault does not calculate a price from the records or from the AI reading. The patient pays that fee in euros through PayPal. After the payment is verified, that doctor can open the request.

The doctor has seven days from the payment to reply.

- A submitted opinion keeps 5% for MediVault and sends 95% to the doctor’s PayPal account.
- If the doctor declines, or the seven days pass with no reply, the patient is refunded in full.
- A late opinion after a refund is refused.

The doctor can also ask for a private reading of the submitted images and PDFs. It organizes what is in the file, what was reported, earlier care, an impression, and points worth a closer look. The patient never sees that reading. The opinion they receive is the one the doctor writes. The reading does not change the fee.

Patients and doctors can see the reply date on the request. The notification bell reports a payment, a reply, or a refund.

## How to run

You need Docker with Compose. PayPal is used in sandbox mode, in EUR. An OpenAI key is needed only for the doctor’s private reading. The consultation still works without it.

1. Copy the template and fill in the keys. Leave the file named `.env` in the project root. Compose reads it, and it is not committed.

   ```bash
   cp .env.local .env
   ```

   ```
   PAYPAL_CLIENT_ID=
   PAYPAL_CLIENT_SECRET=
   OPENAI_API_KEY=
   ```

   `OPENAI_MODEL` is optional. It defaults to `gpt-4o-mini`.

2. Build and start the stack.

   ```bash
   docker compose up -d --build
   ```

3. Open the app at [http://localhost:4200](http://localhost:4200). The API listens on [http://localhost:8080](http://localhost:8080). The app proxies `/api` to it.

The first start applies the database migrations. Postgres keeps its data in the `pgdata` volume. Uploaded files go to the local S3-compatible store.

| Service | Address |
| --- | --- |
| Web | http://localhost:4200 |
| API | http://localhost:8080 |
| Postgres | localhost:5432 |
| Object storage | http://localhost:9000 |

### Accounts

Sign in as the local admin to approve a doctor before that doctor can be asked for an opinion.

- Email: `admin@medivault.com`
- Password: `admin`

Then create a patient and a doctor from **Create account**. Approve the doctor under **Approvals**. The doctor enters the second-opinion fee and the PayPal email that should receive the 95%.

A patient can upload PDF, JPEG, PNG, or WebP files up to 5 MB, then ask a doctor from the menu. Payment uses a PayPal sandbox buyer. Closing PayPal’s own window leaves the request unpaid, so it can be paid or deleted later.

To stop the stack without deleting data:

```bash
docker compose down
```

## Layout

- `backend` is the ASP.NET Core 10 API. It stores accounts, records, and requests in Postgres, files in object storage, and talks to PayPal Orders, PayPal Payouts, and the model.
- `frontend` is the Angular app, built and served by nginx.
- `docker-compose.yml` runs Postgres 18, object storage, the API, and the web app.

## Where this can go

The library is the part that should stay with the patient. Later, a hospital, clinic, or lab could deliver a report straight into that library when the patient allows it. From there, MediVault can grow toward finding a doctor, booking a visit, and sharing only the records that visit needs.

The aim is that a patient should not have to carry their history from place to place. It should already be there.
