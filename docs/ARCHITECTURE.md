# البنية التقنية والواجهات البرمجية

## الخادم (backend/src/SalesSystem.Api)

| المجلد | المحتوى |
|---|---|
| `Domain/` | الكيانات والتعدادات |
| `Data/` | `AppDbContext` (العلاقات، الفهارس، القيود)، `Migrations/` لـ PostgreSQL، و`SqliteAppDbContext` مع `SqliteMigrations/` للاختبارات والتجربة السريعة، `DbSeeder` (المدير فقط، بلا بيانات تجريبية) |
| `Services/` | منطق الأعمال: `OrderService`، `EmployeeService`، `CatalogService` (أنواع الطلبات والخدمات وطرق الدفع)، `RentalService`، `DashboardService`، `ReportService`، `ReportExporter` (Excel/PDF)، `AuditService`، `SettingsService`، `Calculators` |
| `Controllers/` | طبقة HTTP رفيعة تستدعي الخدمات |
| `Infrastructure/` | JWT، المستخدم الحالي، معالجة الأخطاء، الخيارات، توقيت العمل (Asia/Riyadh)، `RequestLoggingMiddleware` (سجل منظم لكل طلب) |
| `Assets/Fonts/` | خط IBM Plex Sans Arabic (رخصة OFL) لتقارير PDF |

### الجداول

| الجدول | الوصف |
|---|---|
| `Users` / `Roles` | الحسابات (كلمات مرور مُجزّأة) والأدوار `SuperAdmin` و`Employee` |
| `EmployeeProfiles` | ملف الموظف المرتبط بحساب واحد (`UserId` فريد) ورقم وظيفي فريد و`LoginSlug` فريد لرابط الدخول الخاص |
| `EmployeeDeductionRates` | سجل نسب الاستقطاع لكل موظف؛ نسبة واحدة سارية (`EffectiveToUtc IS NULL`) |
| `OrderTypes` | أنواع الطلبات (مبيعات افتراضياً) قابلة للإضافة من الإعدادات |
| `Services` | قائمة الخدمات، مع سعر مقترح وربط اختياري بنوع طلب |
| `PaymentMethods` | `1 = Cash` نقداً، `2 = POS` شبكة/بطاقة — مفتاح أجنبي من الطلبات والدفعات |
| `Orders` | العملية: الموظف، النوع، تاريخ العملية، طريقة الدفع، المبلغ، ولقطة النسبة والاستقطاع والصافي |
| `OrderItems` | خدمات الطلب (`ServiceTypeId` + لقطة اسم الخدمة + الوصف + المبلغ) |
| `OrderPayments` | دفعة الطلب، تُلغى (Voided) ولا تُحذف |
| `DeductionRecords` | سجل الاستقطاع لكل طلب (النسبة، الأساس، القيمة، طريقة التطبيق) |
| `RentalContracts` / `RentalPayments` | الإيجارات ودفعاتها (رقم مرجعي، تاريخ، المستخدم المسجِّل، مرفق اختياري) |
| `AuditLogs` | سجل التدقيق |
| `SystemSettings` | إعدادات الاستقطاع |

أبرز القيود والفهارس:
- `Users.Username` فريد، و`Users.Email` فريد عند وجوده.
- `Orders.OrderNumber` فريد، و`Orders.IdempotencyKey` فريد عند وجوده، وفهارس `(EmployeeId, OperationDate)` و`OperationDate` و`Status`.
- `OrderTypes.Code`/`Name` و`Services.Name` و`PaymentMethods.Code` فريدة.
- `OrderPayments`: فهرس فريد مُرشَّح على `SalesOrderId WHERE Status='Completed'` يمنع أكثر من دفعة فعّالة لكل طلب.
- `DeductionRecords.SalesOrderId` فريد (سجل واحد لكل طلب).
- `RentalPayments.IdempotencyKey` فريد عند وجوده.
- قيود CHECK: المبالغ موجبة، والنسب بين 0 و100، والاستقطاع لا يتجاوز المبلغ.
- جميع العلاقات `ON DELETE RESTRICT`، ولا توجد عمليات حذف للسجلات المالية في الواجهة البرمجية.

### الأمان

- كلمات المرور مُجزّأة بـ PBKDF2 عبر `PasswordHasher` من ASP.NET Identity، مع سياسة: 8 أحرف على الأقل تجمع حروفاً وأرقاماً.
- JWT (HS256) يحمل `security_stamp`، ويتحقق الخادم في كل طلب من أن المستخدم نشط وأن الختم مطابق. لذلك يُبطل التعطيل أو إعادة تعيين كلمة المرور أو تغييرها جميع الجلسات فوراً.
- قفل الحساب 15 دقيقة بعد 5 محاولات فاشلة، وحدّ معدل لطلبات الدخول لكل IP.
- سياسة افتراضية تشترط تسجيل الدخول لكل Endpoint، ثم `[Authorize(Roles=...)]` لكل وحدة.
- التحقق من المدخلات في الخادم (DataAnnotations مع فحوص الخدمات: نوع طلب وخدمة مفعّلان ومتوافقان، طريقة دفع موجودة، تاريخ عملية غير مستقبلي)، ورسائل أخطاء بالعربية بصيغة ProblemDetails. اخترنا DataAnnotations بديلاً عن FluentValidation لأنها كافية لهذه القواعد وتُدمج تلقائياً مع `[ApiController]`.
- السجلات منظمة (Structured Logging): JSON في الإنتاج مع Scopes تحمل `RequestId` و`UserId`، وسطر لكل طلب API (الطريقة، المسار، الحالة، المدة) دون تسجيل الاستعلامات أو أجسام الطلبات.
- عند تسجيل الخروج أو الدخول بحساب آخر يُمسح كاش TanStack Query بالكامل فلا تظهر بيانات المستخدم السابق.
- المرفقات: قائمة امتدادات مسموحة، وفحص التوقيع الثنائي للملف، وحد 5MB، وأسماء عشوائية خارج مجلد الويب، وتنزيل عبر Endpoint محمي.
- ترويسات أمان (`nosniff`، `X-Frame-Options: DENY`، `Referrer-Policy`)، وHSTS في الإنتاج.
- يُحفظ الرمز في الواجهة داخل `sessionStorage` فيُمحى بإغلاق التبويب.

### سجل التدقيق

تُكتب سجلات التدقيق في نفس الـ Transaction مع التغيير الذي تصفه، وتشمل: الدخول (الناجح والفاشل)، الإنشاء، التعديل (القيم قبل وبعد)، الاعتماد، الإلغاء مع السبب، التفعيل والتعطيل، إعادة تعيين كلمة المرور، تعديل نسب الاستقطاع، الأرشفة، إلغاء الدفعات، وتصدير التقارير.

## الواجهات البرمجية

| الطريقة | المسار | الصلاحية |
|---|---|---|
| POST | `/api/auth/login` `{username, password}` أو `{portal, password}` | عام (محدود المعدل) |
| GET | `/api/auth/portal/{slug}` (اسم صاحب رابط الدخول) | عام (محدود المعدل) |
| GET | `/api/auth/me` | مسجّل |
| PUT | `/api/auth/me` `{email, phone}` (بيانات التواصل فقط) | مسجّل |
| POST | `/api/auth/change-password` | مسجّل |
| GET | `/api/dashboard/admin?from&to` | مدير |
| GET | `/api/dashboard/employee` | موظف |
| GET | `/api/orders?search&from&to&employeeId&status&paymentMethod&orderTypeId&serviceTypeId&sortBy&sortDir&page&pageSize` | مدير (الكل) / موظف (طلباته) |
| GET | `/api/catalog/order-types` · `/api/catalog/services?orderTypeId` · `/api/catalog/payment-methods` | مسجّل (`includeInactive` للمدير فقط) |
| POST/PUT | `/api/catalog/order-types[/{id}]` · `/api/catalog/services[/{id}]` | مدير |
| GET | `/api/orders/{id}` | مدير / الموظف المالك |
| GET | `/api/orders/deduction-info?employeeId` | مدير / موظف (نفسه) |
| POST | `/api/orders` `{orderTypeId, operationDate, items[{serviceTypeId, description, unitPrice}], paymentMethod, notes, employeeId?}` (ترويسة `Idempotency-Key`) | مدير / موظف |
| POST | `/api/orders/{id}/approve` | مدير |
| POST | `/api/orders/{id}/cancel` `{reason}` | مدير |
| GET/POST | `/api/employees` | مدير |
| GET/PUT | `/api/employees/{id}` | مدير |
| GET | `/api/employees/lookup` | مدير |
| POST | `/api/employees/{id}/status` `{isActive}` | مدير |
| POST | `/api/employees/{id}/reset-password` | مدير |
| POST | `/api/employees/{id}/login-link` (رابط دخول جديد يُبطل القديم) | مدير |
| PUT | `/api/employees/{id}/deduction` `{percentage, notes}` | مدير |
| GET | `/api/employees/{id}/deduction-history` · `/stats` · `/orders` | مدير |
| GET/POST | `/api/rentals` · GET `/api/rentals/summary` | مدير |
| GET/PUT | `/api/rentals/{id}` · POST `/api/rentals/{id}/archive?archived=` | مدير |
| POST | `/api/rentals/{id}/payments` (multipart + `Idempotency-Key`) | مدير |
| POST | `/api/rentals/{id}/payments/{pid}/void` | مدير |
| GET | `/api/rentals/{id}/payments/{pid}/attachment` | مدير |
| GET | `/api/reports/{Sales\|Employees\|Cash\|Card\|Deductions\|Rentals\|Operations\|Performance\|Comprehensive}?from&to&employeeId&groupBy=day\|week\|month\|year&format=json\|xlsx\|pdf` | مدير |
| GET | `/api/audit-logs?search&action&entityType&from&to&page` | مدير |
| GET/PUT | `/api/settings/deduction` | مدير |

## حالات الطلب والإيجار

- **الطلب**: يبدأ طلب الموظف بحالة `Pending` (قيد المراجعة)، ثم يعتمده المدير فيصبح `Completed`، أو يلغيه فيصبح `Cancelled`. الطلب الذي ينشئه المدير يُسجَّل `Completed` مباشرة. الطلبات الملغاة لا تدخل في الإجماليات.
- **تاريخ العملية** (`OperationDate`): التاريخ التجاري للعملية، وعليه تُبنى الفلاتر واللوحات والتقارير. لا يُقبل تاريخ مستقبلي، ولا يستطيع الموظف تسجيل عملية أقدم من 7 أيام (المدير بلا قيد).
- **الإيجار** (تُحسب الحالة عند كل قراءة):
  - مدفوع: المدفوع ≥ القيمة.
  - متأخر: تجاوز تاريخ الاستحقاق دون سداد كامل.
  - مدفوع جزئياً: يوجد سداد جزئي قبل تاريخ الاستحقاق.
  - مستحق: خلال 7 أيام من تاريخ الاستحقاق.
  - قادم: ما عدا ذلك.

  تُسلسَل الدفعات المتزامنة على الإيجار نفسه بقفل على صف الإيجار، فلا يمكن تجاوز المبلغ المتبقي.

## تعريف مؤشرات لوحة المدير

| المؤشر | التعريف |
|---|---|
| إجمالي المبيعات | مجموع مبالغ الطلبات غير الملغاة التي يقع تاريخ عمليتها في الفترة |
| المبالغ المحصّلة | مبالغ الطلبات المعتمدة (`Completed`) في الفترة |
| إجمالي الاستقطاعات | مجموع قيم الاستقطاع المحفوظة في الطلبات |
| الإيجارات المدفوعة | دفعات سداد الإيجارات المسجلة في الفترة |
| الإيجارات المستحقة | المتبقي غير المسدد من جميع الإيجارات غير المؤرشفة |
| مقارنة الأداء | الفترة المختارة مقابل فترة مماثلة الطول تسبقها مباشرة |
