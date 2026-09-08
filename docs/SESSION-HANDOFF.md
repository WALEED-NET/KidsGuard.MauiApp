<div dir="rtl">

# 📘 KidsGuard — تسليم الجلسة الأولى

> **تاريخ الجلسة:** 9 سبتمبر 2026  
> **الحالة:** التخطيط مكتمل — لم يُكتب سطر كود بعد  
> **الغرض:** نقل كامل السياق لجلسة جديدة في `VS Code`  
> **الخطة التفصيلية:** [PROJECT-PLAN.md](./PROJECT-PLAN.md)

---

## 📊 الملخص التنفيذي

| المقياس | القيمة |
|---------|--------|
| **المشروع** | تطبيق رقابة أبوية خاص للعائلة — غير منشور على المتجر |
| **المسار** | `C:\Users\SlyAdmin\Documents\Visual Studio 2017\projects\KidsGuard` |
| **`git`** | ✅ مُهيّأ، فرع `main`، أوّل `commit` هو `4636d69` |
| **الحالة الحالية** | مجلد فيه `docs` و`README.md` و`.gitignore` فقط |
| **الخطوة التالية** | المهمّة `1.0` — إنشاء مشروع `net10.0-android` وأوّل بناء |
| **البيئة** | ✅ جاهزة بالكامل (تفصيلها أدناه) |

---

## 🎯 ما يريده المستخدم

بدأ السؤال عن أفضل برامج الرقابة الأبوية الجاهزة، ثمّ انتقل لبناء واحد بنفسه. المطلوب بكلماته:

> «أريد التطبيق أثبّته على جهاز الطفل، أعطيه كلّ الصلاحيات، وتلقائياً أقدر أتحكّم في وقت التشغيل وحجب بعض البرامج وحجب بعض المواقع.»

ثمّ أضاف طلبين:

1. لوحة للوالد يرى فيها الإحصائيات ويمدّد الوقت عن بُعد.
2. **ثغرة خروج مضمونة** لحذف التطبيق إن حصل خلل أو أراد إنهاء الموضوع.

---

## ✅ القرارات النهائية

| القرار | الاختيار | البديل المرفوض ولماذا |
|--------|----------|------------------------|
| المنصّة | `Android` فقط، `minSdk 26` / `targetSdk 35` | `iOS` — `Apple` تمنع كلّ هذه الوظائف تقنياً |
| تطبيق الطفل | `.NET for Android` خام (`net10.0-android`) | `MAUI UI` / `Blazor Hybrid` — التطبيق `services` بلا واجهة، و`WebView` عبء يُسرّع قتل العملية |
| الصلاحيات | `Device Owner` عبر `dpm set-device-owner` | `DeviceAdmin` وحده — رادع لا مانع |
| حجب التطبيقات | `setPackagesSuspended` | `AccessibilityService` — أصعب، ويستوجب إفصاحاً خاصاً في `Google Play` |
| حجب المواقع | `VpnService` محليّ يفلتر `DNS` | خدمة خارجية — لا داعي |
| الخادم | `ASP.NET Core API` + `SignalR Hub` على `MonsterASP` (مجاني) | `Firebase` — تبيّن أنّه غير مطلوب |
| لوحة الوالد | `Blazor WebAssembly PWA` بتصميم `mobile-first` | تطبيق `Android` ثانٍ — واجهة إضافية بلا داعٍ |
| | | `Blazor Server` — لا يصير `PWA` حقيقياً، ويستهلك ذاكرة الخادم المحدودة |
| إشعار الوالد | `WhatsApp` عبر الخادم | `FCM` — يضيف اعتماداً على `Google` بلا حاجة |
| التوزيع | `APK` مباشر | `Google Play` — سياسات صارمة بلا فائدة لمشروع عائلي |
| بيئة الاختبار | `emulator` للمراحل 1 و2، جهاز حقيقي بعدها | جهاز حقيقي من البداية — خطر العلوق |

---

## 🔄 تصحيحات جوهرية حدثت أثناء الجلسة

> ⚠️ **اقرأ هذا القسم قبل أي شيء.** هذه اعتقادات قيلت ثمّ ثبت خطؤها بالبحث. لا تعُد إليها.

| # | ما قيل أولاً ❌ | الصواب ✅ | الأثر |
|---|-----------------|-----------|-------|
| 1 | `Device Owner` تتطلّب إعادة ضبط مصنع | تتطلّب فقط **عدم وجود حسابات لحظة التنفيذ**. تُحذف الحسابات مؤقّتاً وتُعاد بعدها، والبيانات كلّها تبقى | غيّر المشروع كلّياً — من «مستحيل» إلى «خمس دقائق» |
| 2 | تحتاج لاب توب وكابل | تطبيق `LADB` ينفّذ `adb` على الجهاز نفسه عبر `Wireless debugging` (`Android 11+`) | لا كمبيوتر إطلاقاً |
| 3 | `Firebase` مطلوب للإشعارات | جهاز الطفل `ForegroundService` أصلاً، فـ`SignalR` يبقى متّصلاً. والوالد يُوقَظ برسالة `WhatsApp` | صفر `Firebase` |
| 4 | نبني `AccessibilityService` للحجب | `setPackagesSuspended` من `Device Owner` أبسط وأقوى | حذف أصعب مرحلة |
| 5 | `Device Owner` تُؤجَّل للنهاية | تُنفَّذ من البداية لأنها تُبسّط لا تُعقّد | إعادة ترتيب المراحل |

---

## 🚪 المبدأ الأهمّ في المشروع

بعد أن يصير التطبيق `Device Owner`، **هو وحده** يملك التخلّي عن الملكية. فإن تعطّل، عَلِق الجهاز. لذلك:

### ستّ طبقات خروج

| # | الطريقة | تعمل حتى لو |
|---|---------|-------------|
| 1 | زرّ «فكّ الحماية» بـ`PIN` | الوضع طبيعي |
| 2 | `Activity` طوارئ مستقلّة | الواجهة الرئيسية تعطّلت |
| 3 | رمز سرّي في لوحة الاتصال (`SECRET_CODE`) | التطبيق لا يفتح أصلاً |
| 4 | رسالة `SMS` من رقم الوالد | الجهاز بعيد |
| 5 | حارس حلقة الانهيار (يفكّ تلقائياً بعد الانهيار الثالث) | ينهار عند كلّ إقلاع |
| 6 | **إعادة ضبط المصنع** | كلّ ما سبق فشل |

### 🔴 قواعد لا تُخالَف

| # | القاعدة | السبب |
|---|---------|-------|
| 1 | **`DISALLOW_FACTORY_RESET` ممنوع نهائياً** | هو ما يحوّل الجهاز إلى لِبنة عند أي خلل. تركُه مفتوحاً هو الضمان الوحيد |
| 2 | **ترتيب الفكّ**: القيود ← فكّ التجميد ← فتح الحذف ← `ClearDeviceOwnerApp` **أخيراً** | بعد `ClearDeviceOwnerApp` تُفقد الصلاحية اللازمة لرفع القيود، فتبقى للأبد |
| 3 | **نقطة خروج واحدة** | الطبقات الستّ تستدعي دالّة `ReleaseEverything` نفسها |
| 4 | **لا نلمس جهاز الطفل قبل نجاح المرحلة 1 على `emulator`** | نختبر آلية عدم العلوق حيث العلوق آمن |
| 5 | **الطفل يعلم بوجود التطبيق** | المراقبة السرّية تنكشف وتدمّر الثقة |
| 6 | **قفل خيارات المطوّر مؤجّل ومغلق افتراضياً** | يبقى `adb` مخرجاً إضافياً حتى يثبت الاستقرار |

</div>

<div dir="ltr">

```csharp
// ═══════════════════════════════════════════════════════════════
// الترتيب الإلزامي — أي انحراف عنه يُعلّق الجهاز
// ═══════════════════════════════════════════════════════════════
void ReleaseEverything()
{
    dpm.ClearUserRestriction(admin, UserManager.DisallowSafeBoot);
    dpm.ClearUserRestriction(admin, UserManager.DisallowAddUser);
    dpm.ClearUserRestriction(admin, UserManager.DisallowDebuggingFeatures);
    dpm.ClearUserRestriction(admin, UserManager.DisallowInstallUnknownSources);

    dpm.SetPackagesSuspended(admin, blockedPackages, suspended: false);
    dpm.SetUninstallBlocked(admin, PackageName, false);

    dpm.ClearDeviceOwnerApp(PackageName);   // ⭐ أخيراً فقط
}
```

</div>

<div dir="rtl">

---

## 🖥️ حالة البيئة (مفحوصة فعلياً)

| المكوّن | الحالة |
|---------|--------|
| `.NET SDK` | ✅ `10.0.302` (ومعه `7.0` و`8.0` و`9.0`) |
| `android workload` | ✅ `36.1.53/10.0.100` — `net10.0-android` جاهز |
| `Android SDK` | ✅ `C:\Program Files (x86)\Android\android-sdk` |
| `platforms` | ✅ `android-35`، `android-36` |
| `emulator` | ✅ `emulator.exe` موجود |
| `AVDs` جاهزة | `pixel_7_-_api_35_0`، `pixel_7_-_api_36_0` |
| `system images` | ⚠️ **`google_apis_playstore` فقط** |
| `git identity` | ✅ `Waleed Bensumaidea` / `waleed.com145@gmail.com` |
| متغيّرات البيئة | ⚠️ `ANDROID_HOME` و`ANDROID_SDK_ROOT` غير مضبوطين |

### ⚠️ مسألة صورة الـ`emulator`

الصور المثبّتة كلّها `playstore`، وهي لا تقبل `adb root` وقد تعترض على `Device Owner`.

| الخيار | التكلفة | القرار |
|--------|---------|--------|
| تجربة `AVD` الموجود أوّلاً | دقيقتان | ⭐ نبدأ به — الجهاز الجديد بلا حساب فقد ينجح |
| تنزيل `system-images;android-35;google_apis;x86_64` | ~`1.5GB` | لو فشل الأوّل |

---

## 🧩 تقسيم المرحلة الأولى إلى مهامّ صغيرة

| # | المهمّة | معيار القبول |
|---|---------|---------------|
| **1.0** | `solution` + مشروع `net10.0-android`، `minSdk 26` / `targetSdk 35` | التطبيق يفتح على الـ`emulator` |
| **1.1** | `AppDeviceAdminReceiver` + `device_admin.xml` + تسجيله في الـ`manifest` | شاشة تعرض «`Device Owner`: لا» |
| **1.2** | 🔬 تجربة `dpm set-device-owner` على الـ`AVD` الحالي | الشاشة تعرض «نعم» — أو نعرف أنّنا نحتاج صورة `google_apis` |
| **1.3** | ⭐ `ReleaseManager` بالترتيب الصحيح + سجلّ لكلّ خطوة | تُستدعى بلا `Device Owner` فلا ترمي استثناءً |
| **1.4** | الطبقة 1: زرّ «فكّ الحماية» بـ`PIN` | `snapshot` ← تفعيل ← فكّ ← التطبيق قابل للحذف |
| **1.5** | الطبقة 2: `Activity` طوارئ مستقلّة | تعمل مع تعطيل الواجهة الرئيسية عمداً |
| **1.6** | الطبقة 3: `SECRET_CODE` في لوحة الاتصال | 🔬 نتحقّق عملياً أنّه ما زال يُسلَّم في الإصدارات الحديثة |
| **1.7** | الطبقة 4: أمر عبر `SMS` | إرسال رسالة للـ`emulator` تفكّ الحماية |
| **1.8** | الطبقة 5: حارس حلقة الانهيار | زرع انهيار متعمّد ← فكّ تلقائي بعد الثالث |
| **1.9** | اختبار تكامل: كلّ طبقة على `snapshot` نظيف | الطبقات الخمس تنجح منفردة |

> 🔬 المهامّ `1.2` و`1.6` **استكشافية** — نتيجتها قد تغيّر التصميم، ولذلك وُضعت مبكّرة.

</div>

<div dir="ltr">

```bash
# snapshot workflow for every Device Owner experiment
adb emu avd snapshot save clean_state
# ... run the experiment ...
adb emu avd snapshot load clean_state
```

</div>

<div dir="rtl">

---

## 🗺️ المراحل الثماني (نظرة عامة)

| المرحلة | المحتوى | الحالة |
|---------|---------|--------|
| 1 | ⭐ مخرج الطوارئ بطبقاته الستّ | ⬜ التالي |
| 2 | `Device Owner` + الأذونات الصامتة | ⬜ |
| 3 | وقت الاستخدام (`UsageStatsManager`) + الحدود | ⬜ |
| 4 | حجب التطبيقات (`setPackagesSuspended`) + جدول أوقات | ⬜ |
| 5 | حجب المواقع (`VpnService`) | ⬜ |
| 6 | شاشة الإعدادات على جهاز الطفل بـ`PIN` | ⬜ |
| 7 | الخادم: `API` + `SignalR` + طابور أوامر على `MonsterASP` | ⬜ |
| 8 | لوحة الوالد: `Blazor WASM PWA` + `WhatsApp` | ⬜ |

> ℹ️ المراحل 1 إلى 6 تجعل جهاز الطفل يعمل كاملاً **بلا إنترنت ولا خادم**. المرحلتان 7 و8 طبقة فوقها؛ سقوط الخادم لا يفكّ قيداً واحداً.

---

## ❌ ما استُبعد نهائياً

| المستبعَد | السبب |
|-----------|-------|
| **مشاهدة الشاشة المباشرة** | `Android 14+` يشترط موافقة لكل جلسة، و`Android 15 QPR1` يعرض شريطاً بارزاً يوقفها، **والبثّ يتوقّف تلقائياً عند قفل الشاشة** |
| `iOS` | ممنوع من `Apple`؛ `FamilyControls` يحتاج استئذاناً ولا يمنح شيئاً مما نريد |
| `Firebase` / `FCM` | غير مطلوب — انظر التصحيح رقم 3 |
| `AccessibilityService` | استُغني عنه بـ`setPackagesSuspended` |
| النشر على `Google Play` | مشروع عائلي؛ `APK` مباشر يُلغي كلّ عقبات السياسات |
| `DISALLOW_FACTORY_RESET` | يقتل مخرج الطوارئ الأخير |

---

## ❓ أسئلة ما زالت مفتوحة

| # | السؤال | الافتراض الحالي |
|---|--------|------------------|
| 1 | اسم الحزمة النهائي (`package id`) | `com.kidsguard.app` — تغييره لاحقاً يستوجب إعادة تفعيل `Device Owner` |
| 2 | عمر الطفل | يؤثّر على القيم الافتراضية في المرحلة 3 فقط، لا يعطّل البدء |
| 3 | هل يوجد جهاز حقيقي احتياطي؟ | نفترض `emulator` حتى المرحلة 3 |

---

## 🔍 مراجع البحث المعتمدة

| الموضوع | المرجع |
|---------|--------|
| `Device Owner` بلا إعادة ضبط | [Ice-Box-Docs](https://github.com/heruoxin/Ice-Box-Docs/blob/master/Device%20Owner%20(Non%20Root)%20Setup.md) · [Jason Bayton](https://bayton.org/android/android-enterprise-faq/can-i-set-device-owner-without-factory-reset/) |
| `adb` بلا كمبيوتر | [LADB](https://github.com/tytydraco/ladb) · [XDA](https://www.xda-developers.com/your-phone-can-run-adb-on-itself-with-no-pc/) |
| قيود `MediaProjection` | [Android 14 behavior changes](https://developer.android.com/about/versions/14/behavior-changes-14) · [Media projection](https://developer.android.com/media/grow/media-projection) |
| سياسة `AccessibilityService` | [Play Console Help](https://support.google.com/googleplay/android-developer/answer/16585319?hl=en) |
| `Device Owner` رسمياً | [AOSP provisioning](https://source.android.com/docs/devices/admin/provision) · [TestDPC](https://github.com/googlesamples/android-testdpc) |
| مشاريع مشابهة | [childscreentime/cst](https://github.com/childscreentime/cst) · [personalDNSfilter](https://f-droid.org/en/packages/dnsfilter.android/) |
| البديل المدمج | [Android 16 QPR2 supervision](https://support.google.com/android/answer/16766047?hl=en-IN) |

---

## 📝 ملاحظة تستحقّ التذكّر

`Android 16 QPR2` (ديسمبر 2025) أضاف `local supervision` مدمجاً في النظام: رقابة بـ`PIN` محليّ، وحدود يومية ولكلّ تطبيق، وفلترة محتوى الويب، وبريد استرداد يمنع تجاوزها بإعادة الضبط.

**إن كان جهاز الطفل يستقبل هذا التحديث، فنصف ما سنبنيه متوفّر مجاناً وبحماية أقوى.** يبقى المشروع مفيداً للتحكّم الدقيق وللأجهزة التي لا تستقبله.

---

**آخر تحديث:** 9 سبتمبر 2026

</div>
