# ADR-0002: Appointments, patient portal and multi-facility structure

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Klinova 1.0 covered walk-in patients: registration, admission with a daily queue
number, examination, billing and reports. Institutions also need:

- **Online appointments** that patients in Türkiye already know from the central
  appointment system MHRS (Merkezi Hekim Randevu Sistemi) and from university
  hospital portals: choose province, facility, department and physician, see free
  slots, cancel or change the appointment, join a waiting list.
- **Rules against wasted slots.** MHRS restricts patients who neither attend nor
  cancel: no new appointment in the same department for 15 days, extended to all
  departments on repetition; cancellation is only possible until a deadline.
- **Several sites** (hospital buildings, branch clinics) managed by one installation.
- **Working hours, leave and holidays** per physician as the source of free slots.

## Decision

1. **Facilities.** A `Facility` has a province and a district; every polyclinic
   belongs to one facility and can be opened or closed for online booking.
   Existing installations are migrated by attaching current polyclinics to a
   default facility.
2. **Schedules as the source of truth.** Weekly `WorkSchedule` blocks (day, time
   range, slot length, validity, online or desk-only), `DoctorLeave` and `Holiday`
   (full or half day) are expanded on demand into free slots by a pure
   `SlotCalculator`. Slots are not stored; only appointments are.
3. **Appointment integrity in the database.** A filtered unique index on
   `(DoctorId, Date, StartTime)` for non-cancelled appointments makes double
   booking impossible even under concurrent requests; the service turns the
   violation into a friendly message.
4. **Configurable MHRS-style policy.** `AppointmentPolicy` (horizon, lead time,
   per-polyclinic and per-patient limits, cancellation deadline, no-show
   restriction and escalation, reminders, waiting list) is bound from
   configuration. Online bookings are subject to all limits; staff bookings
   (phone / desk) may book further ahead and are not blocked by the online-only
   restrictions.
5. **Check-in creates a visit.** Arriving appointment patients are admitted with
   one click; the existing queue and examination workflow is reused.
6. **Outbox notifications.** Messages are stored in the same transaction as the
   change that caused them and delivered by a background worker through pluggable
   senders (SMTP, HTTP webhook for SMS operators, logging fallback) with retries.
7. **Patient portal in the same application.** Patients sign in with their
   national id and a password (role `Patient`). Staff and patient areas are
   separated by authorization policies: the fallback policy requires a staff role.
   An existing patient file is linked to a new portal account only if name, birth
   date **and the phone number on file** match, because a national id, name and
   birth date are not secrets in Türkiye.

## Consequences

- Institutions can run online appointments without a separate product, and the
  appointment, clinical and billing data stay consistent.
- Real SMS delivery requires an adapter to the institution's SMS operator; e-Devlet
  sign-in and SMS one-time codes are on the roadmap.
- Background jobs run inside the web process. When several instances run behind a
  load balancer, jobs should be enabled on one instance only
  (`Klinova:BackgroundJobs:Enabled`).
- Klinova is not connected to MHRS itself; integrations with Ministry of Health
  systems remain optional future modules.
