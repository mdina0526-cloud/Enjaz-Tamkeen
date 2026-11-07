export type Role = 'SuperAdmin' | 'Employee'
export type OrderStatus = 'Pending' | 'Completed' | 'Cancelled'
export type PaymentMethod = 'Cash' | 'Card'
export type PaymentStatus = 'Completed' | 'Voided'
export type RentalStatus = 'Upcoming' | 'Due' | 'PartiallyPaid' | 'Paid' | 'Overdue'
export type RentalPropertyType = 'Property' | 'Office' | 'Site' | 'Other'
export type DeductionTreatment = 'SubtractFromNet' | 'RecordOnly'
export type ReportType = 'Sales' | 'Employees' | 'Cash' | 'Card' | 'Deductions' | 'Rentals' | 'Operations' | 'Performance' | 'Comprehensive'

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface UserInfo {
  id: number
  username: string
  fullName: string
  email: string | null
  phone: string | null
  role: Role
  employeeId: number | null
  employeeCode: string | null
  jobTitle: string | null
  lastLoginAtUtc: string | null
}

export interface LoginResponse {
  token: string
  expiresAtUtc: string
  user: UserInfo
}

export interface EmployeeListItem {
  id: number
  userId: number
  employeeCode: string
  fullName: string
  username: string
  email: string | null
  phone: string | null
  jobTitle: string | null
  isActive: boolean
  deductionPercentage: number
  ordersCount: number
  totalSales: number
  lastLoginAtUtc: string | null
  createdAtUtc: string
}

export interface EmployeeDetail {
  id: number
  userId: number
  employeeCode: string
  fullName: string
  username: string
  email: string | null
  phone: string | null
  jobTitle: string | null
  hireDate: string | null
  isActive: boolean
  deductionPercentage: number
  lastLoginAtUtc: string | null
  createdAtUtc: string
  loginSlug: string | null
}

export interface LoginPortal {
  fullName: string
  jobTitle: string | null
}

export interface EmployeeLookup {
  id: number
  fullName: string
  employeeCode: string
  isActive: boolean
}

export interface EmployeeStats {
  ordersCount: number
  totalSales: number
  totalDeductions: number
  totalNet: number
  cashTotal: number
  cardTotal: number
  cancelledCount: number
}

export interface DeductionRule {
  id: number
  percentage: number
  effectiveFromUtc: string
  effectiveToUtc: string | null
  notes: string | null
  createdBy: string | null
}

export interface OrderListItem {
  id: number
  orderNumber: string
  operationDate: string
  createdAtUtc: string
  employeeId: number
  employeeName: string
  orderTypeId: number
  orderTypeName: string
  status: OrderStatus
  paymentMethod: PaymentMethod
  serviceName: string | null
  itemsCount: number
  subtotal: number
  deductionPercentage: number
  deductionAmount: number
  netAmount: number
}

export interface OrderItem {
  id: number
  serviceTypeId: number
  serviceName: string
  description: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface Payment {
  id: number
  amount: number
  method: PaymentMethod
  status: PaymentStatus
  paidAtUtc: string
  voidedAtUtc: string | null
}

export interface OrderDetail {
  id: number
  orderNumber: string
  operationDate: string
  createdAtUtc: string
  employeeId: number
  employeeName: string
  employeeCode: string
  orderTypeId: number
  orderTypeName: string
  status: OrderStatus
  paymentMethod: PaymentMethod
  subtotal: number
  deductionPercentage: number
  deductionAmount: number
  netAmount: number
  deductionTreatment: DeductionTreatment | null
  notes: string | null
  completedAtUtc: string | null
  cancelledAtUtc: string | null
  cancellationReason: string | null
  items: OrderItem[]
  payments: Payment[]
}

export interface OrderTypeItem {
  id: number
  code: string
  name: string
  isActive: boolean
  sortOrder: number
}

export interface ServiceItem {
  id: number
  name: string
  description: string | null
  defaultPrice: number | null
  orderTypeId: number | null
  orderTypeName: string | null
  isActive: boolean
  sortOrder: number
}

export interface PaymentMethodItem {
  id: PaymentMethod
  code: string
  name: string
  isActive: boolean
}

export interface DeductionInfo {
  employeeId: number
  employeeName: string
  percentage: number
  enabled: boolean
  treatment: DeductionTreatment
  label: string
}

export interface DeductionSettings {
  enabled: boolean
  treatment: DeductionTreatment
  label: string
}

export interface RentalListItem {
  id: number
  title: string
  propertyType: RentalPropertyType
  location: string | null
  beneficiaryName: string
  amount: number
  paidAmount: number
  remainingAmount: number
  dueDate: string
  status: RentalStatus
  isArchived: boolean
  paymentsCount: number
}

export interface RentalPayment {
  id: number
  amount: number
  method: PaymentMethod
  paidOn: string
  reference: string | null
  notes: string | null
  hasAttachment: boolean
  attachmentName: string | null
  isVoided: boolean
  voidReason: string | null
  createdAtUtc: string
}

export interface RentalDetail {
  id: number
  title: string
  propertyType: RentalPropertyType
  location: string | null
  beneficiaryName: string
  beneficiaryPhone: string | null
  amount: number
  paidAmount: number
  remainingAmount: number
  dueDate: string
  status: RentalStatus
  notes: string | null
  isArchived: boolean
  createdAtUtc: string
  payments: RentalPayment[]
}

export interface RentalSummary {
  total: number
  upcoming: number
  due: number
  partiallyPaid: number
  paid: number
  overdue: number
  totalAmount: number
  totalPaid: number
  totalRemaining: number
  overdueAmount: number
}

export interface KeyValueAmount {
  key: string
  amount: number
  count: number
}

export interface PeriodTotals {
  from: string
  to: string
  totalSales: number
  ordersCount: number
  totalCollected: number
  totalDeductions: number
  totalNet: number
}

export interface EmployeePerformance {
  employeeId: number
  employeeName: string
  employeeCode: string
  ordersCount: number
  totalSales: number
  totalDeductions: number
  totalNet: number
}

export interface AdminDashboard {
  totalSales: number
  ordersCount: number
  totalCollected: number
  totalDeductions: number
  totalNet: number
  totalRentalPayments: number
  totalRentals: number
  rentalsOutstanding: number
  activeEmployees: number
  pendingOrders: number
  overdueRentals: number
  previous: PeriodTotals | null
  dailySales: KeyValueAmount[]
  monthlySales: KeyValueAmount[]
  byEmployee: KeyValueAmount[]
  byPaymentMethod: KeyValueAmount[]
  byService: KeyValueAmount[]
  employeePerformance: EmployeePerformance[]
  recentOrders: OrderListItem[]
}

export interface EmployeeDashboard {
  todayOrders: number
  todaySales: number
  ordersCount: number
  totalSales: number
  totalCollected: number
  totalDeductions: number
  totalNet: number
  pendingOrders: number
  completedOrders: number
  cancelledOrders: number
  deductionPercentage: number
  deductionLabel: string
  dailySales: KeyValueAmount[]
  byPaymentMethod: KeyValueAmount[]
  recentOrders: OrderListItem[]
}

export interface AuditLog {
  id: number
  createdAtUtc: string
  username: string | null
  action: string
  entityType: string
  entityId: string | null
  details: string | null
  ipAddress: string | null
}

export interface ReportColumn {
  key: string
  title: string
  kind: 'text' | 'money' | 'number'
}

export interface ReportResult {
  title: string
  period: string
  generatedAtUtc: string
  summary: { label: string; value: string }[]
  sections: { title: string; columns: ReportColumn[]; rows: (string | number | null)[][] }[]
}
