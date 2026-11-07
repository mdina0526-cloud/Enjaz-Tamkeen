import type { DeductionTreatment, OrderStatus, PaymentMethod, RentalPropertyType, RentalStatus } from '@/types/api'

export const orderStatusLabel: Record<OrderStatus, string> = {
  Pending: 'قيد المراجعة',
  Completed: 'مكتمل',
  Cancelled: 'ملغي',
}

export const paymentMethodLabel: Record<PaymentMethod, string> = {
  Cash: 'نقداً',
  Card: 'شبكة / بطاقة (POS)',
}

export const rentalStatusLabel: Record<RentalStatus, string> = {
  Upcoming: 'قادم',
  Due: 'مستحق',
  PartiallyPaid: 'مدفوع جزئياً',
  Paid: 'مدفوع',
  Overdue: 'متأخر',
}

export const propertyTypeLabel: Record<RentalPropertyType, string> = {
  Property: 'عقار',
  Office: 'مكتب',
  Site: 'موقع',
  Other: 'أخرى',
}

export const treatmentLabel: Record<DeductionTreatment, string> = {
  SubtractFromNet: 'يُطرح من المبلغ (الصافي = المبلغ − الاستقطاع)',
  RecordOnly: 'للتسجيل فقط (الصافي = المبلغ)',
}

export const auditActionLabel: Record<string, string> = {
  Login: 'تسجيل دخول',
  LoginFailed: 'محاولة دخول فاشلة',
  Create: 'إنشاء',
  Update: 'تعديل',
  Approve: 'اعتماد',
  Cancel: 'إلغاء',
  Activate: 'تفعيل',
  Deactivate: 'تعطيل',
  ResetPassword: 'إعادة تعيين كلمة المرور',
  ChangePassword: 'تغيير كلمة المرور',
  ChangeDeduction: 'تعديل نسبة الاستقطاع',
  Archive: 'أرشفة',
  Void: 'إلغاء دفعة',
  Export: 'تصدير تقرير',
}

export const entityTypeLabel: Record<string, string> = {
  User: 'مستخدم',
  Employee: 'موظف',
  SalesOrder: 'طلب مبيعات',
  Rental: 'إيجار',
  RentalPayment: 'دفعة إيجار',
  SystemSettings: 'إعدادات النظام',
  Report: 'تقرير',
  OrderType: 'نوع طلب',
  Service: 'خدمة',
}
