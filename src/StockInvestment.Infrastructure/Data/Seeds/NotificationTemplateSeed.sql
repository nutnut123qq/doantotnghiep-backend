-- Seed NotificationTemplates for Alert Notifications
-- Run this after migration to ensure Slack/Telegram notifications work

-- Price Alert Template
INSERT INTO "NotificationTemplates" ("Id", "Name", "EventType", "Subject", "Body", "IsActive", "CreatedAt", "UpdatedAt")
VALUES (
  gen_random_uuid(),
  'ALERT_PRICE_TRIGGERED',
  1, -- NotificationEventType.PriceAlert
  'Price Alert: {Symbol}',
  '🔔 Price Alert Triggered

Stock: {Symbol}
Condition: Price {Operator} {Threshold}
Current Price: {CurrentValue}
Time: {Time}

💡 {AiExplanation}

---
Stock Investment Platform',
  true,
  NOW(),
  NOW()
)
ON CONFLICT DO NOTHING;

-- Volume Alert Template  
INSERT INTO "NotificationTemplates" ("Id", "Name", "EventType", "Subject", "Body", "IsActive", "CreatedAt", "UpdatedAt")
VALUES (
  gen_random_uuid(),
  'ALERT_VOLUME_TRIGGERED',
  5, -- NotificationEventType.VolumeAlert (adjust based on your enum)
  'Volume Alert: {Symbol}',
  '🔔 Volume Alert Triggered

Stock: {Symbol}
Threshold: {Threshold}
Current Volume: {CurrentValue}
Time: {Time}

💡 {AiExplanation}

---
Stock Investment Platform',
  true,
  NOW(),
  NOW()
)
ON CONFLICT DO NOTHING;

-- Verify templates were created
SELECT "Name", "EventType", "IsActive" FROM "NotificationTemplates" WHERE "Name" LIKE 'ALERT_%';
