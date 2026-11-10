# نشر النظام على خادم Linux

هذه الحزمة تشغّل الواجهة وAPI وPostgreSQL عبر Docker Compose. تُفتح الواجهة على نطاق واحد؛ يخدم Caddy الملفات الثابتة، ويوجّه `/api` إلى الخادم، ويُصدر شهادة HTTPS ويجدّدها تلقائياً.

## المتطلبات

- خادم Linux عليه Docker Engine وDocker Compose.
- نطاق موجّه إلى عنوان الخادم، مع فتح المنفذين 80 و443. لا تنشر منفذ قاعدة البيانات أو API للعامة.
- مساحة تخزين دائمة للقاعدة والمرفقات والنسخ الاحتياطية.

## الإعداد والتشغيل

```bash
cp deploy/production.env.example .env.production
chmod 600 .env.production
# عدّل القيم الست داخل .env.production ثم:
docker compose --env-file .env.production -f compose.production.yml up -d --build
docker compose --env-file .env.production -f compose.production.yml ps
```

`SITE_DOMAIN` اسم النطاق بدون `https://`. يجب أن يكون `JWT_KEY` عشوائياً بطول 32 حرفاً على الأقل، و`ADMIN_INITIAL_PASSWORD` قوية. كلمة مرور المدير الأولى تُستخدم عند إنشاء الحساب فقط؛ تغييرها في الملف لاحقاً لا يغيّر حساباً موجوداً. يحفظ Compose PostgreSQL والمرفقات وشهادات HTTPS في وحدات تخزين دائمة.

زر الموقع يعمل من `https://<SITE_DOMAIN>` بعد اكتمال شهادة HTTPS. فحص Docker للخادم يستخدم `/health/ready` الذي يتحقق من اتصال قاعدة البيانات؛ `/health/live` يفحص عملية الخادم فقط. تُعاد الخدمات تلقائياً بعد إعادة تشغيل Docker أو الخادم.

## النسخ الاحتياطي

```bash
bash deploy/backup.sh
```

ينشئ السكربت نسخة PostgreSQL بصيغة `pg_dump -Fc` ونسخة من المرفقات ومفاتيح حماية أسرار التحقق بخطوتين في `backups/<وقت UTC>/`. احتفظ بنسخة أخرى خارج الخادم، وجدول التشغيل اليومي، وجرّب الاستعادة على بيئة منفصلة. استعادة قاعدة موجودة تستبدل بياناتها، لذا يجب مراجعة النسخة ومسار الهدف قبل استخدام `pg_restore`.

لجدولة نسخة يومية عند 02:00 على الخادم، نفّذ `mkdir -p backups` ثم أضف إلى `crontab -e` بعد استبدال مسار المشروع:

```cron
0 2 * * * cd /srv/enjaz-tamkeen && /usr/bin/bash deploy/backup.sh >> /srv/enjaz-tamkeen/backups/backup.log 2>&1
```

احفظ نسخاً خارج الخادم أيضاً، لأن عطل القرص قد يفقد المشروع والنسخ المحلية معاً.

عند ضياع تطبيق المصادقة لحساب المدير، يمكن إعادة ضبط كلمة مرور قوية عبر أمر `--reset-superadmin` داخل حاوية الخادم؛ هذا يمسح إعداد التحقق بخطوتين ويُنهي الجلسات القديمة. اضبط متغيرَي `RESET_SUPERADMIN_USERNAME` و`RESET_SUPERADMIN_PASSWORD` مؤقتاً عند تنفيذ الأمر، ولا تحفظ كلمة المرور في المستودع.

## التحديث

```bash
git pull
docker compose --env-file .env.production -f compose.production.yml up -d --build
```

قبل تحديث نسخة الإنتاج، خذ نسخة احتياطية، ثم راجع `docker compose ... ps` وسجلات `backend` و`web`. لم تُنفّذ هذه الحزمة على خادم فعلي بعد؛ النشر يحتاج نطاقاً وخادماً وملف أسرار خاصاً بالبيئة.
