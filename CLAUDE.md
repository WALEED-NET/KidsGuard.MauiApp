# KidsGuard — سياق المشروع

تطبيق رقابة أبوية خاص للعائلة على `Android`. غير منشور على المتجر — يُوزّع بـ`APK` مباشر.

## اقرأ أولاً

| الملف | متى |
|-------|-----|
| [docs/SESSION-HANDOFF.md](docs/SESSION-HANDOFF.md) | ⭐ في بداية أي جلسة جديدة — يحوي القرارات والتصحيحات وحالة البيئة |
| [docs/PROJECT-PLAN.md](docs/PROJECT-PLAN.md) | الخطة التفصيلية والمعمارية والمراحل الثماني |

## البنية المستهدفة

| القطعة | التقنية |
|--------|---------|
| جهاز الطفل | `.NET for Android` (`net10.0-android36.0`)، `minSdk 26` / `targetSdk 36` |
| الخادم | `ASP.NET Core API` + `SignalR Hub` على `MonsterASP` (مجاني) |
| لوحة الوالد | `Blazor WebAssembly PWA`، `mobile-first` |
| إشعار الوالد | `WhatsApp` عبر الخادم — **لا `Firebase` ولا `FCM`** |

## 🔴 قواعد لا تُخالَف

1. **`DISALLOW_FACTORY_RESET` ممنوع نهائياً.** هو الفرق بين جهاز نختبر عليه وجهاز نخسره.
2. **ترتيب فكّ `Device Owner`**: القيود ← فكّ التجميد ← فتح الحذف ← `ClearDeviceOwnerApp` **أخيراً**. بعد الأخير تُفقد الصلاحية اللازمة لما قبله.
3. **نقطة خروج واحدة**: كلّ طبقات الطوارئ الستّ تستدعي `ReleaseEverything` نفسها.
4. **لا نلمس جهاز الطفل قبل نجاح المرحلة 1 على `emulator`** مع `snapshot` قبل كل تجربة.
5. **قفل خيارات المطوّر مؤجّل** ومغلق افتراضياً — يبقى `adb` مخرجاً إضافياً.
6. **الطفل يعلم بوجود التطبيق.** لا مراقبة سرّية.

## 🚫 مستبعد نهائياً — لا تقترحه

`iOS` · مشاهدة الشاشة المباشرة · `Firebase`/`FCM` · `AccessibilityService` · النشر على `Google Play` · `Blazor Hybrid` على جهاز الطفل

## الحالة الآن

لم يُكتب كود. الخطوة التالية: المهمّة `1.0` — إنشاء مشروع `net10.0-android` وأوّل بناء على `emulator`.

## 📚 تعليمات إلزامية خارجية

مستودع القواعد المشتركة: `D:\projects\ai-coding-guidelines` — **يُقرأ المنطبق منه قبل كتابة أي كود.**

| الملف | متى يُقرأ |
|-------|-----------|
| `.github/copilot-instructions.md` | القواعد العامة — مرجع دائم |
| `.github/instructions/csharp.instructions.md` | قبل أي ملف `.cs` |
| `.maui-skills/plugins/maui-skills/skills/maui-current-apis/SKILL.md` | حارس دائم ضدّ `API` مهجور |
| `.maui-skills/.../xamarin-android-migration/SKILL.md` | بنية `csproj` و`manifest` لـ`.NET for Android` |
| `.maui-skills/.../maui-permissions/SKILL.md` | عند أي `runtime permission` |
| `.maui-skills/.../maui-platform-invoke/SKILL.md` | عند استدعاء `API` أصلي من `Android` |
| `.maui-skills/.../maui-app-lifecycle/SKILL.md` | عند `Service` أو دورة حياة |
| `.maui-skills/.../maui-secure-storage/SKILL.md` | عند تخزين `PIN` أو أسرار |
| `.maui-skills/.../maui-unit-testing/SKILL.md` | عند كتابة اختبارات |

### ما ينطبق وما لا ينطبق

جهاز الطفل مشروع `.NET for Android` **خام لا `MAUI`** — فلا تنطبق مهارات `XAML` و`Shell` و`CollectionView`
و`data-binding`. ينطبق منها: `platform-invoke` و`permissions` و`app-lifecycle` و`secure-storage`
و`current-apis`. أمّا مهارات `Blazor` في `docs/blazor/` فتنطبق على لوحة الوالد في المرحلة 8.

### قواعد `C#` مأخوذة من المستودع

| القاعدة | التطبيق هنا |
|---------|--------------|
| لا `DateTime.Now` مباشرة | استخدم `TimeProvider` — حدود الوقت اليومية تحتاج اختباراً |
| `_camelCase` للحقول الخاصة، `PascalCase` للباقي | كما هو |
| لا `Exception` للأخطاء المتوقّعة | `ReleaseManager` يرجع نتيجة، لا يرمي |
| اقرأ الملف قبل تعديله | كما هو |
| لا تُنشئ ملفات `.md` بلا طلب صريح | كما هو |
