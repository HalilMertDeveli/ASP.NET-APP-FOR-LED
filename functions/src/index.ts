import {initializeApp} from "firebase-admin/app";
import {getFirestore, FieldValue, Timestamp} from "firebase-admin/firestore";
import {onRequest} from "firebase-functions/v2/https";
import {defineSecret} from "firebase-functions/params";
import {logger} from "firebase-functions";
import {Resend} from "resend";

initializeApp();

const resendApiKey = defineSecret("RESEND_API_KEY");
const ingestSecret = defineSecret("SUPPORT_INGEST_SECRET");
const notifyEmail = defineSecret("SUPPORT_NOTIFY_EMAIL");
const fromEmail = defineSecret("SUPPORT_FROM_EMAIL");

const ALLOWED_SYSTEMS = new Set(["Colorlight", "NovaStar", "Huidu", "Diğer"]);

type SupportPayload = {
  name?: string;
  company?: string | null;
  email?: string;
  phone?: string | null;
  system?: string;
  subject?: string;
  message?: string;
  website?: string | null;
  clientIp?: string | null;
  userAgent?: string | null;
};

function badRequest(res: any, message: string) {
  res.status(400).json({ok: false, error: message});
}

function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

function sanitize(value: unknown, max: number): string {
  return String(value ?? "").trim().slice(0, max);
}

/**
 * HTTPS endpoint: ASP.NET backend posts validated support requests here.
 * Auth: SUPPORT_INGEST_SECRET header (X-Support-Secret).
 * Writes Firestore supportRequests + sends email via Resend.
 */
export const submitSupportRequest = onRequest(
  {
    region: "europe-west1",
    cors: false,
    secrets: [resendApiKey, ingestSecret, notifyEmail, fromEmail],
    timeoutSeconds: 30,
    memory: "256MiB",
  },
  async (req, res) => {
    if (req.method !== "POST") {
      res.status(405).json({ok: false, error: "Method not allowed"});
      return;
    }

    const provided =
      req.get("x-support-secret") ||
      req.get("X-Support-Secret") ||
      "";
    if (!provided || provided !== ingestSecret.value()) {
      res.status(401).json({ok: false, error: "Unauthorized"});
      return;
    }

    const body = (req.body || {}) as SupportPayload;

    // Honeypot — pretend success
    if (body.website && String(body.website).trim().length > 0) {
      logger.warn("Honeypot triggered");
      res.status(200).json({ok: true, id: "ignored"});
      return;
    }

    const name = sanitize(body.name, 120);
    const company = sanitize(body.company, 160) || null;
    const email = sanitize(body.email, 200).toLowerCase();
    const phone = sanitize(body.phone, 40) || null;
    const system = sanitize(body.system, 40);
    const subject = sanitize(body.subject, 200);
    const message = sanitize(body.message, 4000);
    const clientIp = sanitize(body.clientIp, 80) || null;
    const userAgent = sanitize(body.userAgent, 300) || null;

    if (!name || name.length < 2) {
      badRequest(res, "Invalid name");
      return;
    }
    if (!email || !isValidEmail(email)) {
      badRequest(res, "Invalid email");
      return;
    }
    if (!ALLOWED_SYSTEMS.has(system)) {
      badRequest(res, "Invalid system");
      return;
    }
    if (!subject || subject.length < 3) {
      badRequest(res, "Invalid subject");
      return;
    }
    if (!message || message.length < 20) {
      badRequest(res, "Invalid message");
      return;
    }

    const db = getFirestore();

    // Simple rate limit: max 5 requests / IP / 15 minutes
    if (clientIp) {
      const since = Timestamp.fromDate(new Date(Date.now() - 15 * 60 * 1000));
      const recent = await db
        .collection("supportRequests")
        .where("clientIp", "==", clientIp)
        .where("createdAt", ">=", since)
        .limit(6)
        .get();
      if (recent.size >= 5) {
        res.status(429).json({ok: false, error: "Rate limit exceeded"});
        return;
      }
    }

    const docRef = db.collection("supportRequests").doc();
    const createdAt = FieldValue.serverTimestamp();
    const record = {
      name,
      company,
      email,
      phone,
      system,
      subject,
      message,
      clientIp,
      userAgent,
      createdAt,
      emailSent: false,
      source: "aspnet-web",
      status: "new",
    };

    await docRef.set(record);

    const to = notifyEmail.value() || "halilmertdeveliii@gmail.com";
    const from = fromEmail.value() || "LED Teknik Destek <onboarding@resend.dev>";
    const requestId = docRef.id;
    const when = new Date().toISOString();

    const textBody = [
      "Yeni LED teknik destek talebi",
      "----------------------------------------",
      `Talep ID     : ${requestId}`,
      `Talep tarihi : ${when}`,
      `Ad Soyad     : ${name}`,
      `Firma adı    : ${company ?? "-"}`,
      `E-posta      : ${email}`,
      `Telefon      : ${phone ?? "-"}`,
      `Sistem       : ${system}`,
      `Konu         : ${subject}`,
      "",
      "Sorun açıklaması:",
      message,
      "----------------------------------------",
    ].join("\n");

    try {
      const resend = new Resend(resendApiKey.value());
      const result = await resend.emails.send({
        from,
        to: [to],
        replyTo: email,
        subject: `[LED Teknik Destek] Yeni Talep - ${subject}`,
        text: textBody,
      });

      if (result.error) {
        logger.error("Resend error", result.error);
        await docRef.update({
          emailSent: false,
          emailError: String(result.error.message || "resend_error"),
        });
        res.status(502).json({ok: false, error: "Email delivery failed"});
        return;
      }

      await docRef.update({
        emailSent: true,
        emailProviderId: result.data?.id ?? null,
        emailedAt: FieldValue.serverTimestamp(),
      });

      res.status(200).json({ok: true, id: requestId});
    } catch (err) {
      logger.error("Email send failed", err);
      await docRef.update({
        emailSent: false,
        emailError: "exception",
      });
      res.status(502).json({ok: false, error: "Email delivery failed"});
    }
  }
);
