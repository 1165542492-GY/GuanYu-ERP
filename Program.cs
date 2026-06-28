using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Linq;
using System.Windows.Forms;

namespace SupplierErpApp
{
    public sealed class BusinessException : Exception
    {
        public int StatusCode { get; private set; }
        public BusinessException(string message, int statusCode = 400) : base(message) { StatusCode = statusCode; }
    }

    public sealed class EditConflictException : Exception
    {
        public EditConflictException() : base("这条数据已被其他人修改，请刷新后再编辑。") { }
    }

    public sealed class JsonCodec
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        public string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }

    public class Supplier
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Company { get; set; }
        public string Contact { get; set; }
        public string Phone { get; set; }
        public string Goods { get; set; }
        public string Address { get; set; }
        public string Bank { get; set; }
        public string Account { get; set; }
        public string BankNo { get; set; }
        public decimal Payable { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class Customer
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Company { get; set; }
        public string Contact { get; set; }
        public string Phone { get; set; }
        public string Bank { get; set; }
        public string Account { get; set; }
        public string BankNo { get; set; }
        public string Address { get; set; }
        public decimal Receivable { get; set; }
        public string Status { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class Material
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Supplier { get; set; }
        public string NameSpec { get; set; }
        public string QuantityUnit { get; set; }
        public decimal TaxPrice { get; set; }
        public decimal NoTaxPrice { get; set; }
        public string PriceType { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string StockType { get; set; }
        public bool? IsInventoryItem { get; set; }
        public bool IsFinishedGood { get; set; }
        public bool IsServicePart { get; set; }
        public decimal SafetyStock { get; set; }
        public string DefaultWarehouse { get; set; }
        public string CostMethod { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ImportRequest { public string FileName { get; set; } public string Data { get; set; } }
    public class TestDataImportRequest { public string FileName { get; set; } public string Data { get; set; } }
    public class TestDataSheetInfo
    {
        public string SheetName { get; set; }
        public string ModuleKey { get; set; }
        public string ModuleLabel { get; set; }
        public bool Importable { get; set; }
        public bool ReferenceOnly { get; set; }
        public int RowCount { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string[] Errors { get; set; }
    }
    public class TestDataModuleResult
    {
        public string ModuleKey { get; set; }
        public string ModuleLabel { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string[] Errors { get; set; }
    }
    public class TestDataPreviewResult
    {
        public string FileName { get; set; }
        public TestDataSheetInfo[] Sheets { get; set; }
        public string[] UnknownSheets { get; set; }
        public string FileHash { get; set; }
        public int TotalFailed { get; set; }
        public bool CanImport { get; set; }
    }
    public class TestDataRunResult
    {
        public string FileName { get; set; }
        public TestDataModuleResult[] Modules { get; set; }
        public int TotalAdded { get; set; }
        public int TotalUpdated { get; set; }
        public int TotalSkipped { get; set; }
        public int TotalFailed { get; set; }
    }
    public class CsvImportRequest { public string FileName { get; set; } public string Data { get; set; } public Dictionary<string, string> ConflictActions { get; set; } }
    public class TableImportResult
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int FailedRows { get; set; }
        public string[] Errors { get; set; }
        public string[] Warnings { get; set; }
        public bool NeedsConflictDecision { get; set; }
        public string[] Conflicts { get; set; }
    }
    public class BatchDeleteRequest { public string[] Ids { get; set; } }
    public class BatchSupplierRequest { public List<Supplier> Items { get; set; } }
    public class BatchCustomerRequest { public List<Customer> Items { get; set; } }
    public class BatchMaterialRequest { public List<Material> Items { get; set; } }
    public class BatchSalesOrderRequest { public List<SalesOrder> Items { get; set; } }
    public class BatchSalesOutboundRequest { public List<SalesOutbound> Items { get; set; } }
    public class BatchPurchaseOrderRequest { public List<PurchaseOrder> Items { get; set; } }
    public class BatchPurchaseInboundRequest { public List<PurchaseInbound> Items { get; set; } }

    public class FinanceTransaction
    {
        public string Id { get; set; }
        public string Date { get; set; }
        public string AccountType { get; set; }
        public decimal Receipt { get; set; }
        public decimal Payment { get; set; }
        public string PaymentMethod { get; set; }
        public string Purpose { get; set; }
        public string Counterparty { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class OpeningBalances
    {
        public decimal PublicAccount { get; set; }
        public decimal CompanyPrivate { get; set; }
        public decimal PersonalPrivate { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class SystemSettings
    {
        public decimal TaxRate { get; set; }
        public string ClearDataPassword { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class TaxRateRequest { public decimal TaxRate { get; set; } public string UpdatedAt { get; set; } }
    public class ClearTestDataRequest { public string Password { get; set; } public string ConfirmText { get; set; } }
    public class DeepInitializeRequest
    {
        public string Password { get; set; }
        public string ConfirmText { get; set; }
        public bool KeepFinalBackup { get; set; } = true;
        public bool ClearOldBackupFiles { get; set; } = true;
        public bool ClearOperationLogs { get; set; } = true;
        public bool ClearBackupRecords { get; set; } = true;
        public bool ClearTestArtifacts { get; set; } = true;
    }
    public class DeepInitializeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string BackupName { get; set; }
        public DeepInitializeClearedSummary Cleared { get; set; }
        public string[] Preserved { get; set; }
    }
    public class DeepInitializeClearedSummary
    {
        public int BusinessFiles { get; set; }
        public bool OperationLogs { get; set; }
        public bool BackupRecords { get; set; }
        public int OldBackupFiles { get; set; }
        public bool TestArtifacts { get; set; }
    }
    public class ChangeClearDataPasswordRequest { public string OldPassword { get; set; } public string NewPassword { get; set; } public string ConfirmPassword { get; set; } }

    public class BomDetail
    {
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal OriginalUnitPrice
        {
            get { return OriginalPrice; }
            set { OriginalPrice = value; }
        }
        public string PriceType { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NoTaxPrice { get; set; }
        public decimal UnitPriceWithoutTax
        {
            get { return NoTaxPrice; }
            set { NoTaxPrice = value; }
        }
        public decimal Amount { get; set; }
        public string PriceSourceTime { get; set; }
        public string Note { get; set; }
        public bool PriceMissing { get; set; }
    }

    public class BomItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string ModelCode { get; set; }
        public string ModelName { get; set; }
        public string ProductName { get; set; }
        public string Version { get; set; }
        public string Status { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public List<BomDetail> Items { get; set; }
        public bool PriceMissing { get; set; }
        public string MissingPriceMaterials { get; set; }
    }

    public class ModelCost
    {
        public string Id { get; set; }
        public string ModelCode { get; set; }
        public string ModelName { get; set; }
        public string ProductName { get; set; }
        public string BomVersion { get; set; }
        public string BomId { get; set; }
        public string BomCode { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public bool PriceMissing { get; set; }
        public string MissingPriceMaterials { get; set; }
    }

    public class DictionaryOption
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Status { get; set; }
        public int SortOrder { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class ContractSetting
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public bool IsDefault { get; set; }
        public string Status { get; set; }
        public int Sort { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class ContractDetailLine
    {
        public int Seq { get; set; }
        public string DeviceName { get; set; }
        public string ModelSpec { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public bool TaxIncluded { get; set; }
        public decimal TaxRate { get; set; }
        public decimal NoTaxUnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; }
    }

    public class ContractAccessoryLine
    {
        public int Seq { get; set; }
        public string Name { get; set; }
        public string Spec { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
        public string Note { get; set; }
    }

    public class ContractConfigLine
    {
        public int Seq { get; set; }
        public string Category { get; set; }
        public string Item { get; set; }
        public string Brand { get; set; }
        public string Spec { get; set; }
        public string Quantity { get; set; }
        public string Unit { get; set; }
        public string Note { get; set; }
    }

    public class ContractTechParamLine
    {
        public int Seq { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
    }

    public class ContractItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string CustomerCode { get; set; }
        public string PartyAName { get; set; }
        public string PartyAContact { get; set; }
        public string PartyAPhone { get; set; }
        public string PartyAAddress { get; set; }
        public string PartyBName { get; set; }
        public string PartyBContact { get; set; }
        public string PartyBPhone { get; set; }
        public string PartyBAddress { get; set; }
        public string SignDate { get; set; }
        public string TemplateId { get; set; }
        public string TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string TemplateContent { get; set; }
        public string PaymentTerms { get; set; }
        public string DeliveryTime { get; set; }
        public string DeliveryPlace { get; set; }
        public string PackagingMethod { get; set; }
        public string TransportMethod { get; set; }
        public string InvoiceType { get; set; }
        public bool TaxIncluded { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TotalAmount { get; set; }
        public string TotalAmountChinese { get; set; }
        public decimal DepositRatio { get; set; }
        public decimal DepositAmount { get; set; }
        public string DepositAmountChinese { get; set; }
        public decimal BalanceAmount { get; set; }
        public string BalanceAmountChinese { get; set; }
        public int InstallmentMonths { get; set; }
        public decimal InstallmentAmount { get; set; }
        public string InstallmentAmountChinese { get; set; }
        public string InstallmentNote { get; set; }
        public string ReceivingAccount { get; set; }
        public string QualityAcceptanceTerms { get; set; }
        public string AfterSalesTerms { get; set; }
        public string ExcludedWarranty { get; set; }
        public string BreachTerms { get; set; }
        public string InvoiceTitle { get; set; }
        public string TaxNumber { get; set; }
        public string AccountHolder { get; set; }
        public string BankBranch { get; set; }
        public string CompanyAccount { get; set; }
        public string PersonalAccount { get; set; }
        public string BankRoutingNo { get; set; }
        public string Status { get; set; }
        public string InternalNote { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public List<ContractDetailLine> Items { get; set; }
        public List<ContractAccessoryLine> Accessories { get; set; }
        public List<ContractConfigLine> ConfigItems { get; set; }
        public List<ContractTechParamLine> TechParams { get; set; }
    }

    public class ContractPreviewResult { public string Html { get; set; } }

    public class SalesOrder
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string CustomerId { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerContact { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }
        public string ItemType { get; set; }
        public string ModelCostId { get; set; }
        public string BomId { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public decimal TaxExcludedSalePrice { get; set; }
        public decimal TaxIncludedSalePrice { get; set; }
        public decimal TaxExcludedSaleAmount { get; set; }
        public decimal TaxIncludedSaleAmount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string OrderDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class SalesOutbound
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string SalesOrderId { get; set; }
        public string SalesOrderNo { get; set; }
        public string CustomerName { get; set; }
        public string ItemType { get; set; }
        public string ModelCostId { get; set; }
        public string BomId { get; set; }
        public string BomCode { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal CostAmount { get; set; }
        public string OutboundDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class PurchaseOrder
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierName { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string PriceType { get; set; }
        public decimal Amount { get; set; }
        public string OrderDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class PurchaseInbound
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string PurchaseOrderId { get; set; }
        public string PurchaseNo { get; set; }
        public string SupplierName { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public decimal InboundPrice { get; set; }
        public decimal Amount { get; set; }
        public string InboundDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ProductionPick
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string BomId { get; set; }
        public string BomName { get; set; }
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal CostAmount { get; set; }
        public string PickDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class FinishedInbound
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string BomId { get; set; }
        public string BomCode { get; set; }
        public string ModelCostId { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Amount { get; set; }
        public string InboundDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ProductionWorkOrder
    {
        public string Id { get; set; }
        public string WorkOrderNo { get; set; }
        public string WorkOrderDate { get; set; }
        public string SourceType { get; set; }
        public string SalesOrderId { get; set; }
        public string SalesOrderNo { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string ProductName { get; set; }
        public string Spec { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public string BomId { get; set; }
        public string BomName { get; set; }
        public string ModelCostId { get; set; }
        public decimal UnitCost { get; set; }
        public string PlannedStartDate { get; set; }
        public string PlannedFinishDate { get; set; }
        public string ActualStartDate { get; set; }
        public string ActualFinishDate { get; set; }
        public decimal ProducedQuantity { get; set; }
        public decimal UnproducedQuantity { get; set; }
        public decimal PickedMaterialAmount { get; set; }
        public decimal FinishedInboundAmount { get; set; }
        public string Status { get; set; }
        public string Remark { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ProductionWorkOrderFinishRequest
    {
        public decimal FinishQuantity { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class AfterSalesPartLine
    {
        public string MaterialId { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string Remark { get; set; }
    }

    public class AfterSalesServiceOrder
    {
        public string Id { get; set; }
        public string ServiceNo { get; set; }
        public string ServiceDate { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public string MachineName { get; set; }
        public string MachineSpec { get; set; }
        public string FaultDescription { get; set; }
        public string ServiceType { get; set; }
        public string AssignedWorker { get; set; }
        public string VisitDate { get; set; }
        public string RepairResult { get; set; }
        public string Status { get; set; }
        public string Remark { get; set; }
        public decimal PartsAmount { get; set; }
        public decimal LaborAmount { get; set; }
        public decimal OtherAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ReceivableAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal UnreceivedAmount { get; set; }
        public string ReceivableId { get; set; }
        public string ReceivableNo { get; set; }
        public List<AfterSalesPartLine> Parts { get; set; }
        public string CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class ReceiptDetail
    {
        public string Id { get; set; }
        public string ReceiptDate { get; set; }
        public decimal Amount { get; set; }
        public string Account { get; set; }
        public string PaymentMethod { get; set; }
        public string Handler { get; set; }
        public string Note { get; set; }
        public string FinanceTransactionId { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class PaymentDetail
    {
        public string Id { get; set; }
        public string PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Account { get; set; }
        public string PaymentMethod { get; set; }
        public string Handler { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
    }

    public class Receivable
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string CustomerName { get; set; }
        public string SalesOrderId { get; set; }
        public string SalesOrderNo { get; set; }
        public string ServiceOrderId { get; set; }
        public string ServiceOrderNo { get; set; }
        public decimal ReceivableAmount { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal UnreceivedAmount { get; set; }
        public string DueDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string SourceType { get; set; }
        public List<ReceiptDetail> ReceiptDetails { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class Payable
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string SupplierName { get; set; }
        public string PurchaseOrderId { get; set; }
        public string PurchaseNo { get; set; }
        public decimal PayableAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public string DueDate { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string SourceType { get; set; }
        public List<PaymentDetail> PaymentDetails { get; set; }
        public string UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

    public class StockItem
    {
        public string ItemType { get; set; }
        public string ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string Spec { get; set; }
        public string Unit { get; set; }
        public string WarehouseName { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal StockAmount { get; set; }
        public string StockType { get; set; }
        public bool IsFinishedGood { get; set; }
        public bool IsServicePart { get; set; }
        public decimal SafetyStock { get; set; }
        public string StockStatus { get; set; }
        public bool? IsInventoryItem { get; set; }
        public string DefaultWarehouse { get; set; }
        public string CostMethod { get; set; }
        public decimal Quantity { get { return CurrentQuantity; } set { CurrentQuantity = value; } }
    }

    public class StockSummary
    {
        public int ItemCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public StockItem[] Items { get; set; }
    }

    public class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
    public class UserSession { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public bool IsAdmin { get; set; } public string[] Permissions { get; set; } }
    public class UserDef { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public string PasswordHash { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } public string UpdatedAt { get; set; } }
    public class UserPublic { public string Username { get; set; } public string DisplayName { get; set; } public string Role { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } public string PermissionSummary { get; set; } public string UpdatedAt { get; set; } }
    public class CreateUserRequest { public string Username { get; set; } public string Password { get; set; } public string DisplayName { get; set; } public bool Enabled { get; set; } public string[] Permissions { get; set; } }
    public class UpdateUserRequest { public string DisplayName { get; set; } public bool Enabled { get; set; } public string Password { get; set; } public string[] Permissions { get; set; } public string UpdatedAt { get; set; } }
    public class ChangePasswordRequest { public string OldPassword { get; set; } public string NewPassword { get; set; } }
    public class PermissionGroup { public string Module { get; set; } public PermissionItem[] Items { get; set; } }
    public class PermissionItem { public string Key { get; set; } public string Label { get; set; } }

    public static partial class Program
    {
        static readonly object DataLock = new object();
        static readonly object SessionLock = new object();
        static readonly JsonCodec Json = new JsonCodec();
        static readonly Dictionary<string, UserSession> Sessions = new Dictionary<string, UserSession>();
        static List<UserDef> Users = new List<UserDef>();
        const string AdminUsername = "admin";
        const string DefaultClearDataPassword = "88888888";
        const string DefaultAdminPasswordHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";

        static readonly string AppRoot = @"D:\冠誉制造ERP";
        static readonly string DefaultDataDir = Path.Combine(AppRoot, "Data");
        static readonly string LegacyDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "智造ERP供应商管理");
        static string DataDir;
        static string DataDirectorySource = "Default";
        static bool IsDefaultDataDirectory = true;
        static readonly string BackupDir = Path.Combine(AppRoot, "Backups");
        static readonly string ExportsDir = Path.Combine(AppRoot, "Exports");
        static readonly string ImportsDir = Path.Combine(AppRoot, "Imports");
        static string UsersFile;
        static string DataFile;
        static string SupplierSequenceFile;
        static string CustomerFile;
        static string CustomerSequenceFile;
        static string MaterialFile;
        static string MaterialSequenceFile;
        static string FinanceFile;
        static string OpeningFile;
        static string BomFile;
        static string BomSequenceFile;
        static string ModelCostFile;
        static string ModelCostSequenceFile;
        static string SystemSettingsFile;
        static string ContractSettingsFile;
        static string ContractSettingSequenceFile;
        static string ContractsFile;
        static string ContractSequenceFile;
        static string DictionaryOptionsFile;
        static string SalesOrdersFile;
        static string SalesOrderSequenceFile;
        static string SalesOutboundsFile;
        static string SalesOutboundSequenceFile;
        static string PurchaseOrdersFile;
        static string PurchaseOrderSequenceFile;
        static string PurchaseInboundsFile;
        static string PurchaseInboundSequenceFile;
        static string ProductionPicksFile;
        static string ProductionPickSequenceFile;
        static string FinishedInboundsFile;
        static string FinishedInboundSequenceFile;
        static string ProductionWorkOrdersFile;
        static string AfterSalesServiceOrdersFile;
        static string ReceivablesFile;
        static string ReceivableSequenceFile;
        static string PayablesFile;
        static string PayableSequenceFile;
        static string LogFile;
        const int Port = 8787;
        static readonly string DefaultListenUrl = "http://0.0.0.0:" + Port;
        static HttpListener Listener;
        static NotifyIcon TrayIcon;
        static bool StartupBrowserOpened;

        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "SupplierErpApp_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("冠誉制造 ERP 已经在运行。\r\n\r\n请查看系统托盘图标，或先关闭已运行的程序后再启动。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    ResolveDataDirectory(args ?? new string[0]);
                    InitializeDataFilePaths();
                    LogDataDirectoryStartup();
                    EnsureDataDirectories();
                    if (IsDefaultDataDirectory) TryMigrateLegacyData();
                    if (!File.Exists(DataFile)) File.WriteAllText(DataFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(SupplierSequenceFile)) File.WriteAllText(SupplierSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(CustomerFile)) File.WriteAllText(CustomerFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(CustomerSequenceFile)) File.WriteAllText(CustomerSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(MaterialFile)) File.WriteAllText(MaterialFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(MaterialSequenceFile)) File.WriteAllText(MaterialSequenceFile, "0", new UTF8Encoding(false));
                    EnsureLegacyCodes();
                    if (!File.Exists(FinanceFile)) File.WriteAllText(FinanceFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(OpeningFile)) File.WriteAllText(OpeningFile, Json.Serialize(new OpeningBalances()), new UTF8Encoding(false));
                    if (!File.Exists(BomFile)) File.WriteAllText(BomFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(BomSequenceFile)) File.WriteAllText(BomSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(ModelCostFile)) File.WriteAllText(ModelCostFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(SystemSettingsFile)) File.WriteAllText(SystemSettingsFile, Json.Serialize(new SystemSettings { TaxRate = 10, ClearDataPassword = DefaultClearDataPassword }), new UTF8Encoding(false));
                    EnsureSystemSettingsDefaults();
                    if (!File.Exists(ContractSettingsFile)) File.WriteAllText(ContractSettingsFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(ContractSettingSequenceFile)) File.WriteAllText(ContractSettingSequenceFile, "0", new UTF8Encoding(false));
                    if (!File.Exists(ContractsFile)) File.WriteAllText(ContractsFile, "[]", new UTF8Encoding(false));
                    if (!File.Exists(ContractSequenceFile)) File.WriteAllText(ContractSequenceFile, "0", new UTF8Encoding(false));
                    EnsureDefaultDictionaryOptions();
                    EnsureDefaultContractSettings();
                    EnsureBusinessDataFiles();
                    RepairAllSequenceFiles();
                    EnsureUsersFile();
                    LoadUsers();
                    StartServer();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    SetupTray();
                    OpenBrowserOnStartup();
                    Application.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("系统启动失败：\r\n" + ex.Message + "\r\n\r\n请尝试右键选择“以管理员身份运行”。", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    try { if (Listener != null) Listener.Stop(); } catch { }
                    if (TrayIcon != null) TrayIcon.Dispose();
                }
            }
        }

        static string ParseDataDirArgument(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--data-dir", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length) throw new InvalidOperationException("启动参数 --data-dir 缺少目录路径。");
                    return args[i + 1];
                }
                const string prefix = "--data-dir=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(prefix.Length);
            }
            return null;
        }

        static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var full = Path.GetFullPath(path.Trim().Trim('"'));
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        static void ValidateSandboxDataDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("测试沙盒数据目录不能为空。");

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("测试沙盒数据目录无效：" + path);

            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("测试沙盒数据目录不能是磁盘根目录：" + path);

            var blocked = new[]
            {
                NormalizeDirectoryPath(AppRoot),
                NormalizeDirectoryPath(Path.Combine(AppRoot, "App")),
                NormalizeDirectoryPath(Path.Combine(AppRoot, "Backups")),
                NormalizeDirectoryPath(Path.Combine(AppRoot, "Exports")),
                NormalizeDirectoryPath(Path.Combine(AppRoot, "Imports")),
                NormalizeDirectoryPath(DefaultDataDir)
            };
            foreach (var item in blocked)
            {
                if (item != null && string.Equals(path, item, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("测试沙盒数据目录不能使用受保护路径：" + path);
            }
        }

        static void ResolveDataDirectory(string[] args)
        {
            string argPath = ParseDataDirArgument(args);
            string envPath = Environment.GetEnvironmentVariable("ERP_DATA_DIR");
            string sourcePath = null;
            string sourceLabel = "Default";

            if (!string.IsNullOrWhiteSpace(argPath))
            {
                sourcePath = argPath;
                sourceLabel = "Argument:--data-dir";
            }
            else if (!string.IsNullOrWhiteSpace(envPath))
            {
                sourcePath = envPath;
                sourceLabel = "Environment:ERP_DATA_DIR";
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                DataDir = DefaultDataDir;
                DataDirectorySource = "Default";
                IsDefaultDataDirectory = true;
                return;
            }

            var normalized = NormalizeDirectoryPath(sourcePath);
            ValidateSandboxDataDirectory(normalized);
            Directory.CreateDirectory(normalized);
            DataDir = normalized;
            DataDirectorySource = sourceLabel;
            IsDefaultDataDirectory = false;
        }

        static void InitializeDataFilePaths()
        {
            UsersFile = Path.Combine(DataDir, "users.json");
            DataFile = Path.Combine(DataDir, "suppliers.json");
            SupplierSequenceFile = Path.Combine(DataDir, "supplier_sequence.json");
            CustomerFile = Path.Combine(DataDir, "customers.json");
            CustomerSequenceFile = Path.Combine(DataDir, "customer_sequence.json");
            MaterialFile = Path.Combine(DataDir, "materials.json");
            MaterialSequenceFile = Path.Combine(DataDir, "material_sequence.json");
            FinanceFile = Path.Combine(DataDir, "finance.json");
            OpeningFile = Path.Combine(DataDir, "finance_opening.json");
            BomFile = Path.Combine(DataDir, "bom.json");
            BomSequenceFile = Path.Combine(DataDir, "bom_sequence.json");
            ModelCostFile = Path.Combine(DataDir, "model_costs.json");
            ModelCostSequenceFile = Path.Combine(DataDir, "model_cost_sequence.json");
            SystemSettingsFile = Path.Combine(DataDir, "system_settings.json");
            ContractSettingsFile = Path.Combine(DataDir, "contract_settings.json");
            ContractSettingSequenceFile = Path.Combine(DataDir, "contract_setting_sequence.json");
            ContractsFile = Path.Combine(DataDir, "contracts.json");
            ContractSequenceFile = Path.Combine(DataDir, "contract_sequence.json");
            DictionaryOptionsFile = Path.Combine(DataDir, "dictionary_options.json");
            SalesOrdersFile = Path.Combine(DataDir, "sales_orders.json");
            SalesOrderSequenceFile = Path.Combine(DataDir, "sales_order_sequence.json");
            SalesOutboundsFile = Path.Combine(DataDir, "sales_outbounds.json");
            SalesOutboundSequenceFile = Path.Combine(DataDir, "sales_outbound_sequence.json");
            PurchaseOrdersFile = Path.Combine(DataDir, "purchase_orders.json");
            PurchaseOrderSequenceFile = Path.Combine(DataDir, "purchase_order_sequence.json");
            PurchaseInboundsFile = Path.Combine(DataDir, "purchase_inbounds.json");
            PurchaseInboundSequenceFile = Path.Combine(DataDir, "purchase_inbound_sequence.json");
            ProductionPicksFile = Path.Combine(DataDir, "production_picks.json");
            ProductionPickSequenceFile = Path.Combine(DataDir, "production_pick_sequence.json");
            FinishedInboundsFile = Path.Combine(DataDir, "finished_inbounds.json");
            FinishedInboundSequenceFile = Path.Combine(DataDir, "finished_inbound_sequence.json");
            ProductionWorkOrdersFile = Path.Combine(DataDir, "production-work-orders.json");
            AfterSalesServiceOrdersFile = Path.Combine(DataDir, "after-sales-service-orders.json");
            ReceivablesFile = Path.Combine(DataDir, "receivables.json");
            ReceivableSequenceFile = Path.Combine(DataDir, "receivable_sequence.json");
            PayablesFile = Path.Combine(DataDir, "payables.json");
            PayableSequenceFile = Path.Combine(DataDir, "payable_sequence.json");
            LogFile = Path.Combine(DataDir, "operation.log");
        }

        static object GetDataDirectoryInfo()
        {
            return new
            {
                dataDirectory = DataDir,
                dataDirectorySource = DataDirectorySource,
                isDefaultDataDirectory = IsDefaultDataDirectory,
                defaultDataDirectory = DefaultDataDir,
                isSandbox = !IsDefaultDataDirectory
            };
        }

        static void LogDataDirectoryStartup()
        {
            var lines = new List<string>
            {
                "[ERP Startup] DataDirectory=" + DataDir,
                "[ERP Startup] DataDirectorySource=" + DataDirectorySource,
                "[ERP Startup] IsDefaultDataDirectory=" + (IsDefaultDataDirectory ? "true" : "false")
            };
            if (!IsDefaultDataDirectory)
                lines.Add("[ERP Startup] Sandbox/Test Data Directory enabled");
            foreach (var line in lines)
                Console.WriteLine(line);
        }

        static void EnsureDataDirectories()
        {
            Directory.CreateDirectory(AppRoot);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(BackupDir);
            Directory.CreateDirectory(ExportsDir);
            Directory.CreateDirectory(ImportsDir);
            EnsureOperationLogsFile();
        }

        static bool DataDirHasJsonFiles()
        {
            return Directory.Exists(DataDir) && Directory.EnumerateFiles(DataDir, "*.json", SearchOption.TopDirectoryOnly).Any();
        }

        static void CopyLegacyItemIfMissing(string sourcePath, string destPath)
        {
            if (File.Exists(destPath) || Directory.Exists(destPath)) return;
            if (File.Exists(sourcePath)) File.Copy(sourcePath, destPath, false);
            else if (Directory.Exists(sourcePath)) CopyLegacyDirectory(sourcePath, destPath);
        }

        static void CopyLegacyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destDir, dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                Directory.CreateDirectory(target);
            }
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destDir, file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
            }
        }

        static void TryMigrateLegacyData()
        {
            if (!Directory.Exists(LegacyDataDir)) return;
            if (DataDirHasJsonFiles()) return;

            var copiedJson = new List<string>();
            foreach (var src in Directory.EnumerateFiles(LegacyDataDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                string dest = Path.Combine(DataDir, Path.GetFileName(src));
                if (File.Exists(dest)) continue;
                File.Copy(src, dest, false);
                copiedJson.Add(Path.GetFileName(src));
            }

            string legacyLog = Path.Combine(LegacyDataDir, "operation.log");
            if (File.Exists(legacyLog) && !File.Exists(LogFile)) File.Copy(legacyLog, LogFile, false);

            string legacyBackupDir = Path.Combine(LegacyDataDir, "backups");
            if (Directory.Exists(legacyBackupDir))
            {
                foreach (var item in new DirectoryInfo(legacyBackupDir).EnumerateFileSystemInfos())
                    CopyLegacyItemIfMissing(item.FullName, Path.Combine(BackupDir, item.Name));
            }

            if (copiedJson.Count == 0) return;

            string detail = string.Join(", ", copiedJson);
            string message = "已从旧数据目录迁移 JSON 数据：" + LegacyDataDir + " -> " + DataDir + "；文件：" + detail;
            Console.WriteLine(message);
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t系统迁移\t" + message.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        static void SetupTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("打开管理系统", null, delegate { OpenBrowser(); });
            menu.Items.Add("打开数据目录", null, delegate { Process.Start("explorer.exe", DataDir); });
            menu.Items.Add("立即备份", null, delegate { ManualBackup(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出系统", null, delegate { ExitErpApplication(); });
            TrayIcon = new NotifyIcon();
            TrayIcon.Text = "智造ERP供应商管理（运行中）";
            TrayIcon.Icon = System.Drawing.SystemIcons.Application;
            TrayIcon.Visible = true;
            TrayIcon.ContextMenuStrip = menu;
            TrayIcon.DoubleClick += delegate { OpenBrowser(); };
            TrayIcon.ShowBalloonTip(2500, "冠誉制造 ERP 已启动", "本机：http://127.0.0.1:" + Port + "\r\n局域网：http://" + GetLanIp() + ":" + Port, ToolTipIcon.Info);
        }

        static bool ConfirmExitErp()
        {
            string message =
                "• 退出后 ERP 后台服务将停止运行。\r\n" +
                "• 本机浏览器将无法继续访问系统。\r\n" +
                "• 局域网其它电脑也将无法访问系统。\r\n" +
                "• " + Port + " 端口服务将停止。\r\n" +
                "• 当前未保存页面数据可能丢失。\r\n\r\n" +
                "如果只是关闭网页，直接关闭浏览器即可，不要退出 ERP 服务。";
            var result = MessageBox.Show(message, "退出冠誉制造 ERP？", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            return result == DialogResult.OK;
        }

        static void ExitErpApplication()
        {
            if (!ConfirmExitErp()) return;
            if (TrayIcon != null)
            {
                TrayIcon.Visible = false;
                TrayIcon.Dispose();
                TrayIcon = null;
            }
            Application.Exit();
        }

        static void OpenBrowserOnStartup()
        {
            if (StartupBrowserOpened) return;
            StartupBrowserOpened = true;
            OpenBrowser();
        }

        static string ResolveListenUrl()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (!string.IsNullOrWhiteSpace(env))
            {
                var url = env.Trim().TrimEnd('/');
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return url;
            }
            return DefaultListenUrl;
        }

        static string ToHttpListenerPrefix(string listenUrl)
        {
            var url = (listenUrl ?? DefaultListenUrl).Trim().TrimEnd('/');
            if (!url.EndsWith("/", StringComparison.Ordinal)) url += "/";
            return url
                .Replace("http://0.0.0.0:", "http://+:", StringComparison.OrdinalIgnoreCase)
                .Replace("http://*:", "http://+:", StringComparison.OrdinalIgnoreCase);
        }

        static void OpenBrowser()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://127.0.0.1:" + Port + "/",
                UseShellExecute = true
            });
        }

        static void StartServer()
        {
            Listener = new HttpListener();
            string listenUrl = ResolveListenUrl();
            Listener.Prefixes.Add(ToHttpListenerPrefix(listenUrl));
            try
            {
                Listener.Start();
            }
            catch (HttpListenerException ex)
            {
                string hint = ex.ErrorCode == 5
                    ? "请以管理员身份运行「冠誉制造ERP.exe」。"
                    : "端口 " + Port + " 已被占用，或当前用户无权监听该地址。\r\n\r\n请关闭已运行的「冠誉制造ERP」或其他占用该端口的程序后重试。";
                throw new InvalidOperationException("ERP Web 服务启动失败（" + listenUrl + "）。\r\n\r\n" + hint + "\r\n\r\n详细信息：" + ex.Message, ex);
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (Listener.IsListening)
                {
                    try
                    {
                        var context = Listener.GetContext();
                        ThreadPool.QueueUserWorkItem(delegate { Handle(context); });
                    }
                    catch { if (!Listener.IsListening) return; }
                }
            });
        }

        static void Handle(HttpListenerContext ctx)
        {
            BeginAuditContext(ctx);
            try
            {
                AddSecurityHeaders(ctx.Response);
                string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
                if (path == "") { ServeApp(ctx); return; }
                if (path == "/favicon.ico") { ctx.Response.StatusCode = 204; ctx.Response.Close(); return; }
                if (path == "/api/login" && ctx.Request.HttpMethod == "POST") { Login(ctx); return; }
                if (path == "/api/logout" && ctx.Request.HttpMethod == "POST") { Logout(ctx); return; }
                if (path == "/api/system/data-dir" && ctx.Request.HttpMethod == "GET") { WriteJson(ctx, GetDataDirectoryInfo()); return; }
                UserSession user = Authenticate(ctx);
                if (user == null) { WriteJson(ctx, new { error = "请先登录" }, 401); return; }
                if (path == "/api/me") { WriteJson(ctx, user); return; }
                if (path == "/api/permissions" && ctx.Request.HttpMethod == "GET") { WriteJson(ctx, GetPermissionDefinitions()); return; }
                if (path == "/api/me/password" && ctx.Request.HttpMethod == "PUT") { ChangePassword(ctx, user); return; }
                if (path == "/api/users" && ctx.Request.HttpMethod == "GET") { if (!RequireAdmin(ctx, user)) return; ListUsers(ctx); return; }
                if (path == "/api/users" && ctx.Request.HttpMethod == "POST") { if (!RequireAdmin(ctx, user)) return; CreateUser(ctx, user); return; }
                if (path.StartsWith("/api/users/") && ctx.Request.HttpMethod == "PUT") { if (!RequireAdmin(ctx, user)) return; UpdateUser(ctx, user, path.Substring("/api/users/".Length)); return; }
                if (path.StartsWith("/api/users/") && ctx.Request.HttpMethod == "DELETE") { if (!RequireAdmin(ctx, user)) return; DeleteUser(ctx, user, path.Substring("/api/users/".Length)); return; }
                if (path == "/api/info") { WriteJson(ctx, new { ip = GetLanIp(), port = Port, dataDir = DataDir, dataDirectory = DataDir, dataDirectorySource = DataDirectorySource, isDefaultDataDirectory = IsDefaultDataDirectory, isSandbox = !IsDefaultDataDirectory }); return; }
                if (path == "/api/dashboard/business" && ctx.Request.HttpMethod == "GET") { GetBusinessDashboard(ctx, user); return; }
                if (path == "/api/dashboard/owner-summary" && ctx.Request.HttpMethod == "GET") { GetOwnerDashboardSummary(ctx, user); return; }
                if (path == "/api/suppliers" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "supplier.view")) return; WriteJson(ctx, LoadSuppliers()); return; }
                if (path == "/api/suppliers" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.add")) return; AddSupplier(ctx, user); return; }
                if (path.StartsWith("/api/suppliers/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "supplier.edit")) return; UpdateSupplier(ctx, user, path.Substring("/api/suppliers/".Length)); return; }
                if (path.StartsWith("/api/suppliers/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "supplier.delete")) return; DeleteSupplier(ctx, user, path.Substring("/api/suppliers/".Length)); return; }
                if (path == "/api/suppliers/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.import")) return; ImportSuppliers(ctx, user); return; }
                if (path == "/api/suppliers/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.batch_delete")) return; BatchDeleteSuppliers(ctx, user); return; }
                if (path == "/api/suppliers/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "supplier.add")) return; BatchAddSuppliers(ctx, user); return; }
                if (path == "/api/customers" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "customer.view")) return; WriteJson(ctx, LoadCustomers()); return; }
                if (path == "/api/customers" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.add")) return; AddCustomer(ctx, user); return; }
                if (path.StartsWith("/api/customers/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "customer.edit")) return; UpdateCustomer(ctx, user, path.Substring("/api/customers/".Length)); return; }
                if (path.StartsWith("/api/customers/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "customer.delete")) return; DeleteCustomer(ctx, user, path.Substring("/api/customers/".Length)); return; }
                if (path == "/api/customers/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.import")) return; ImportCustomers(ctx, user); return; }
                if (path == "/api/customers/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.batch_delete")) return; BatchDeleteCustomers(ctx, user); return; }
                if (path == "/api/customers/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "customer.add")) return; BatchAddCustomers(ctx, user); return; }
                if (path == "/api/materials" && ctx.Request.HttpMethod == "GET") { if (!HasPermission(user, "material.view") && !HasPermission(user, "bom.view")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadMaterials()); return; }
                if (path == "/api/materials" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.add")) return; AddMaterial(ctx, user); return; }
                if (path.StartsWith("/api/materials/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "material.edit")) return; UpdateMaterial(ctx, user, path.Substring("/api/materials/".Length)); return; }
                if (path.StartsWith("/api/materials/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "material.delete")) return; DeleteMaterial(ctx, user, path.Substring("/api/materials/".Length)); return; }
                if (path == "/api/materials/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.import")) return; ImportMaterials(ctx, user); return; }
                if (path == "/api/materials/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.batch_delete")) return; BatchDeleteMaterials(ctx, user); return; }
                if (path == "/api/materials/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "material.add")) return; BatchAddMaterials(ctx, user); return; }
                if (path == "/api/finance/opening" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.opening_view")) return; WriteJson(ctx, LoadOpeningBalances()); return; }
                if (path == "/api/finance/opening" && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finance.opening_edit")) return; SaveOpeningBalances(ctx, user); return; }
                if (path == "/api/finance" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.view")) return; WriteJson(ctx, LoadFinance()); return; }
                if (path == "/api/finance" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "finance.add")) return; AddFinance(ctx, user); return; }
                if (path == "/api/finance/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "finance.import")) return; ImportFinance(ctx, user); return; }
                if (path == "/api/finance/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finance.import")) return; ExportFinanceTemplateCsv(ctx); return; }
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finance.edit")) return; UpdateFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path.StartsWith("/api/finance/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "finance.delete")) return; DeleteFinance(ctx, user, path.Substring("/api/finance/".Length)); return; }
                if (path == "/api/export") { if (!RequirePermission(ctx, user, "supplier.export")) return; ExportCsv(ctx); return; }
                if (path == "/api/customers/export") { if (!RequirePermission(ctx, user, "customer.export")) return; ExportCustomersCsv(ctx); return; }
                if (path == "/api/materials/export") { if (!RequirePermission(ctx, user, "material.export")) return; ExportMaterialsCsv(ctx); return; }
                if (path == "/api/backup" && ctx.Request.HttpMethod == "POST") { if (!RequireAdmin(ctx, user)) return; string f = ManualBackup(); Audit(user, "手动备份", Path.GetFileName(f)); WriteJson(ctx, new { ok = true, file = f }); return; }
                if (path == "/api/backups" && ctx.Request.HttpMethod == "GET") { ListDataBackups(ctx, user); return; }
                if (path == "/api/backups/create" && ctx.Request.HttpMethod == "POST") { CreateDataBackup(ctx, user); return; }
                if (path == "/api/backups/restore" && ctx.Request.HttpMethod == "POST") { RestoreDataBackup(ctx, user); return; }
                if (path == "/api/backups/open-data-dir" && ctx.Request.HttpMethod == "POST") { OpenBackupDirectory(ctx, user, true); return; }
                if (path == "/api/backups/open-backup-dir" && ctx.Request.HttpMethod == "POST") { OpenBackupDirectory(ctx, user, false); return; }
                if (path == "/api/settings/tax-rate" && ctx.Request.HttpMethod == "GET") { if (!CanReadTaxRate(user)) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadSystemSettings()); return; }
                if (path == "/api/settings/tax-rate" && ctx.Request.HttpMethod == "PUT") { if (!RequireAdmin(ctx, user)) return; SaveTaxRate(ctx, user); return; }
                if (path == "/api/dictionary-options" && ctx.Request.HttpMethod == "GET") { if (!CanReadDictionaryOptions(user)) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } ListDictionaryOptions(ctx); return; }
                if (path == "/api/dictionary-options" && ctx.Request.HttpMethod == "POST") { if (!RequireAdmin(ctx, user)) return; AddDictionaryOption(ctx, user); return; }
                if (path.StartsWith("/api/dictionary-options/") && ctx.Request.HttpMethod == "PUT") { if (!RequireAdmin(ctx, user)) return; UpdateDictionaryOption(ctx, user, path.Substring("/api/dictionary-options/".Length)); return; }
                if (path.StartsWith("/api/dictionary-options/") && ctx.Request.HttpMethod == "DELETE") { if (!RequireAdmin(ctx, user)) return; DeleteDictionaryOption(ctx, user, path.Substring("/api/dictionary-options/".Length)); return; }
                if (path == "/api/bom/export" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "bom.export")) return; ExportBomCsv(ctx); return; }
                if (path == "/api/bom/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "bom.import")) return; ExportBomTemplateCsv(ctx); return; }
                if (path == "/api/bom/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "bom.import")) return; ImportBomCsv(ctx, user); return; }
                if (path == "/api/bom" && ctx.Request.HttpMethod == "GET") { if (!HasPermission(user, "bom.view") && !HasPermission(user, "model_cost.view")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; } WriteJson(ctx, LoadBom()); return; }
                if (path == "/api/bom" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "bom.add")) return; AddBom(ctx, user); return; }
                if (path.StartsWith("/api/bom/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "bom.edit")) return; UpdateBom(ctx, user, path.Substring("/api/bom/".Length)); return; }
                if (path.StartsWith("/api/bom/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "bom.delete")) return; DeleteBom(ctx, user, path.Substring("/api/bom/".Length)); return; }
                if (path == "/api/model-costs/export" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.export")) return; ExportModelCostsCsv(ctx); return; }
                if (path == "/api/model-costs/template" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.import")) return; ExportModelCostTemplateCsv(ctx); return; }
                if (path == "/api/model-costs/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "model_cost.import")) return; ImportModelCostsCsv(ctx, user); return; }
                if (path == "/api/model-costs" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "model_cost.view")) return; WriteJson(ctx, LoadModelCostsWithCurrentPrices()); return; }
                if (path == "/api/model-costs" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "model_cost.add")) return; AddModelCost(ctx, user); return; }
                if (path.StartsWith("/api/model-costs/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "model_cost.edit")) return; UpdateModelCost(ctx, user, path.Substring("/api/model-costs/".Length)); return; }
                if (path.StartsWith("/api/model-costs/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "model_cost.delete")) return; DeleteModelCost(ctx, user, path.Substring("/api/model-costs/".Length)); return; }
                if (path == "/api/contract-settings" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract_setting.view")) return; WriteJson(ctx, LoadContractSettings()); return; }
                if (path == "/api/contract-settings" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract_setting.add")) return; AddContractSetting(ctx, user); return; }
                if (path.StartsWith("/api/contract-settings/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "contract_setting.edit")) return; UpdateContractSetting(ctx, user, path.Substring("/api/contract-settings/".Length)); return; }
                if (path.StartsWith("/api/contract-settings/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "contract_setting.delete")) return; DeleteContractSetting(ctx, user, path.Substring("/api/contract-settings/".Length)); return; }
                if (path == "/api/contracts" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract.view")) return; WriteJson(ctx, LoadContracts()); return; }
                if (path == "/api/contracts" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract.add")) return; AddContract(ctx, user); return; }
                if (path.StartsWith("/api/contracts/") && path.EndsWith("/preview") && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "contract.preview")) return; PreviewContract(ctx, path.Substring("/api/contracts/".Length, path.Length - "/api/contracts/".Length - "/preview".Length)); return; }
                if (path.StartsWith("/api/contracts/") && path.EndsWith("/void") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "contract.void")) return; VoidContract(ctx, user, path.Substring("/api/contracts/".Length, path.Length - "/api/contracts/".Length - "/void".Length)); return; }
                if (path.StartsWith("/api/contracts/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "contract.edit")) return; UpdateContract(ctx, user, path.Substring("/api/contracts/".Length)); return; }
                if (path.StartsWith("/api/contracts/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "contract.delete")) return; DeleteContract(ctx, user, path.Substring("/api/contracts/".Length)); return; }
                if (path == "/api/sales-orders" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "sales_order.view")) return; WriteJson(ctx, LoadSalesOrders()); return; }
                if (path == "/api/sales-orders" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_order.add")) return; AddSalesOrder(ctx, user); return; }
                if (path.StartsWith("/api/sales-orders/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "sales_order.edit")) return; UpdateSalesOrder(ctx, user, path.Substring("/api/sales-orders/".Length)); return; }
                if (path.StartsWith("/api/sales-orders/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "sales_order.delete")) return; DeleteSalesOrder(ctx, user, path.Substring("/api/sales-orders/".Length)); return; }
                if (path == "/api/sales-orders/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_order.delete")) return; BatchDeleteSalesOrders(ctx, user); return; }
                if (path == "/api/sales-orders/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_order.add")) return; BatchAddSalesOrders(ctx, user); return; }
                if (path == "/api/sales-orders/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_order.import")) return; ImportSalesOrders(ctx, user); return; }
                if (path == "/api/sales-orders/export") { if (!RequirePermission(ctx, user, "sales_order.export")) return; ExportSalesOrdersCsv(ctx); return; }
                if (path == "/api/sales-outbounds" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "sales_outbound.view")) return; WriteJson(ctx, LoadSalesOutbounds()); return; }
                if (path == "/api/sales-outbounds" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_outbound.add")) return; AddSalesOutbound(ctx, user); return; }
                if (path.StartsWith("/api/sales-outbounds/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "sales_outbound.edit")) return; UpdateSalesOutbound(ctx, user, path.Substring("/api/sales-outbounds/".Length)); return; }
                if (path.StartsWith("/api/sales-outbounds/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "sales_outbound.delete")) return; DeleteSalesOutbound(ctx, user, path.Substring("/api/sales-outbounds/".Length)); return; }
                if (path == "/api/sales-outbounds/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_outbound.delete")) return; BatchDeleteSalesOutbounds(ctx, user); return; }
                if (path == "/api/sales-outbounds/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_outbound.add")) return; BatchAddSalesOutbounds(ctx, user); return; }
                if (path == "/api/sales-outbounds/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "sales_outbound.import")) return; ImportSalesOutbounds(ctx, user); return; }
                if (path == "/api/sales-outbounds/export") { if (!RequirePermission(ctx, user, "sales_outbound.export")) return; ExportSalesOutboundsCsv(ctx); return; }
                if (path == "/api/purchase-orders" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "purchase_order.view")) return; WriteJson(ctx, LoadPurchaseOrders()); return; }
                if (path == "/api/purchase-orders" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_order.add")) return; AddPurchaseOrder(ctx, user); return; }
                if (path.StartsWith("/api/purchase-orders/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "purchase_order.edit")) return; UpdatePurchaseOrder(ctx, user, path.Substring("/api/purchase-orders/".Length)); return; }
                if (path.StartsWith("/api/purchase-orders/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "purchase_order.delete")) return; DeletePurchaseOrder(ctx, user, path.Substring("/api/purchase-orders/".Length)); return; }
                if (path == "/api/purchase-orders/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_order.delete")) return; BatchDeletePurchaseOrders(ctx, user); return; }
                if (path == "/api/purchase-orders/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_order.add")) return; BatchAddPurchaseOrders(ctx, user); return; }
                if (path == "/api/purchase-orders/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_order.import")) return; ImportPurchaseOrders(ctx, user); return; }
                if (path == "/api/purchase-orders/export") { if (!RequirePermission(ctx, user, "purchase_order.export")) return; ExportPurchaseOrdersCsv(ctx); return; }
                if (path == "/api/purchase-inbounds" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "purchase_inbound.view")) return; WriteJson(ctx, LoadPurchaseInbounds()); return; }
                if (path == "/api/purchase-inbounds" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_inbound.add")) return; AddPurchaseInbound(ctx, user); return; }
                if (path.StartsWith("/api/purchase-inbounds/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "purchase_inbound.edit")) return; UpdatePurchaseInbound(ctx, user, path.Substring("/api/purchase-inbounds/".Length)); return; }
                if (path.StartsWith("/api/purchase-inbounds/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "purchase_inbound.delete")) return; DeletePurchaseInbound(ctx, user, path.Substring("/api/purchase-inbounds/".Length)); return; }
                if (path == "/api/purchase-inbounds/batch-delete" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_inbound.delete")) return; BatchDeletePurchaseInbounds(ctx, user); return; }
                if (path == "/api/purchase-inbounds/batch" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_inbound.add")) return; BatchAddPurchaseInbounds(ctx, user); return; }
                if (path == "/api/purchase-inbounds/import" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "purchase_inbound.import")) return; ImportPurchaseInbounds(ctx, user); return; }
                if (path == "/api/purchase-inbounds/export") { if (!RequirePermission(ctx, user, "purchase_inbound.export")) return; ExportPurchaseInboundsCsv(ctx); return; }
                if (path == "/api/production-picks" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "production_pick.view")) return; WriteJson(ctx, LoadProductionPicks()); return; }
                if (path == "/api/production-picks" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "production_pick.add")) return; AddProductionPick(ctx, user); return; }
                if (path.StartsWith("/api/production-picks/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "production_pick.edit")) return; UpdateProductionPick(ctx, user, path.Substring("/api/production-picks/".Length)); return; }
                if (path.StartsWith("/api/production-picks/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "production_pick.delete")) return; DeleteProductionPick(ctx, user, path.Substring("/api/production-picks/".Length)); return; }
                if (path == "/api/production-work-orders" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "production_pick.view")) return; WriteJson(ctx, LoadProductionWorkOrders()); return; }
                if (path == "/api/production-work-orders" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "production_pick.add")) return; AddProductionWorkOrder(ctx, user); return; }
                if (path.StartsWith("/api/production-work-orders/") && path.EndsWith("/start") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "production_pick.edit")) return; StartProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length, path.Length - "/api/production-work-orders/".Length - "/start".Length)); return; }
                if (path.StartsWith("/api/production-work-orders/") && path.EndsWith("/finish") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "production_pick.edit")) return; FinishProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length, path.Length - "/api/production-work-orders/".Length - "/finish".Length)); return; }
                if (path.StartsWith("/api/production-work-orders/") && path.EndsWith("/cancel") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "production_pick.edit")) return; CancelProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length, path.Length - "/api/production-work-orders/".Length - "/cancel".Length)); return; }
                if (path.StartsWith("/api/production-work-orders/") && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "production_pick.view")) return; GetProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length)); return; }
                if (path.StartsWith("/api/production-work-orders/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "production_pick.edit")) return; UpdateProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length)); return; }
                if (path.StartsWith("/api/production-work-orders/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "production_pick.delete")) return; DeleteProductionWorkOrder(ctx, user, path.Substring("/api/production-work-orders/".Length)); return; }
                if (path == "/api/after-sales-service-orders" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "after_sales.view")) return; WriteJson(ctx, LoadAfterSalesServiceOrders()); return; }
                if (path == "/api/after-sales-service-orders" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.add")) return; AddAfterSalesServiceOrder(ctx, user); return; }
                if (path == "/api/after-sales-service-orders/export") { if (!RequirePermission(ctx, user, "after_sales.view")) return; ExportAfterSalesServiceOrdersCsv(ctx); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/confirm-dispatch") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; ConfirmDispatchAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/confirm-dispatch".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/start") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; StartAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/start".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/finish") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; FinishAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/finish".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/settle") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; SettleAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/settle".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/cancel") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; CancelAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/cancel".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && path.EndsWith("/generate-receivable") && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; GenerateReceivableForAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length, path.Length - "/api/after-sales-service-orders/".Length - "/generate-receivable".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "after_sales.view")) return; GetAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "after_sales.edit")) return; UpdateAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length)); return; }
                if (path.StartsWith("/api/after-sales-service-orders/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "after_sales.delete")) return; DeleteAfterSalesServiceOrder(ctx, user, path.Substring("/api/after-sales-service-orders/".Length)); return; }
                if (path == "/api/finished-inbounds" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "finished_inbound.view")) return; WriteJson(ctx, LoadFinishedInbounds()); return; }
                if (path == "/api/finished-inbounds" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "finished_inbound.add")) return; AddFinishedInbound(ctx, user); return; }
                if (path.StartsWith("/api/finished-inbounds/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "finished_inbound.edit")) return; UpdateFinishedInbound(ctx, user, path.Substring("/api/finished-inbounds/".Length)); return; }
                if (path.StartsWith("/api/finished-inbounds/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "finished_inbound.delete")) return; DeleteFinishedInbound(ctx, user, path.Substring("/api/finished-inbounds/".Length)); return; }
                if (path == "/api/stocks/summary" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "stock.view")) return; WriteJson(ctx, BuildStockSummary()); return; }
                if (path == "/api/stocks" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "stock.view")) return; WriteJson(ctx, BuildStockItems()); return; }
                if (path == "/api/stocks/export") { if (!RequirePermission(ctx, user, "stock.view")) return; ExportStocksCsv(ctx); return; }
                if (path == "/api/receivables" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "receivable.view")) return; WriteJson(ctx, LoadReceivables()); return; }
                if (path == "/api/reconciliation/customers" && ctx.Request.HttpMethod == "GET") { ListReconciliationCustomers(ctx, user); return; }
                if (path == "/api/reconciliation/customer/export" && ctx.Request.HttpMethod == "GET") { ExportCustomerReconciliation(ctx, user); return; }
                if (path == "/api/reconciliation/customer/statement" && ctx.Request.HttpMethod == "GET") { GetCustomerReconciliationStatement(ctx, user); return; }
                if (path == "/api/receivables" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "receivable.add")) return; AddReceivable(ctx, user); return; }
                if (TryHandleReceivableReceiptRoutes(ctx, user, path)) return;
                if (path.StartsWith("/api/receivables/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "receivable.edit")) return; UpdateReceivable(ctx, user, path.Substring("/api/receivables/".Length)); return; }
                if (path.StartsWith("/api/receivables/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "receivable.delete")) return; DeleteReceivable(ctx, user, path.Substring("/api/receivables/".Length)); return; }
                if (path == "/api/payables" && ctx.Request.HttpMethod == "GET") { if (!RequirePermission(ctx, user, "payable.view")) return; WriteJson(ctx, LoadPayables()); return; }
                if (path == "/api/payables" && ctx.Request.HttpMethod == "POST") { if (!RequirePermission(ctx, user, "payable.add")) return; AddPayable(ctx, user); return; }
                if (TryHandlePayablePaymentRoutes(ctx, user, path)) return;
                if (path.StartsWith("/api/payables/") && ctx.Request.HttpMethod == "PUT") { if (!RequirePermission(ctx, user, "payable.edit")) return; UpdatePayable(ctx, user, path.Substring("/api/payables/".Length)); return; }
                if (path.StartsWith("/api/payables/") && ctx.Request.HttpMethod == "DELETE") { if (!RequirePermission(ctx, user, "payable.delete")) return; DeletePayable(ctx, user, path.Substring("/api/payables/".Length)); return; }
                if (path == "/api/admin/clear-test-data" && ctx.Request.HttpMethod == "POST") { ClearTestData(ctx, user); return; }
                if (path == "/api/admin/clear-all-business-data" && ctx.Request.HttpMethod == "POST") { ClearAllBusinessData(ctx, user); return; }
                if (path == "/api/admin/deep-initialize" && ctx.Request.HttpMethod == "POST") { DeepInitializeEmptyDatabase(ctx, user); return; }
                if (path == "/api/admin/clear-data-password" && ctx.Request.HttpMethod == "POST") { ChangeClearDataPassword(ctx, user); return; }
                if (path == "/api/operation-logs/export" && ctx.Request.HttpMethod == "GET") { ExportOperationLogsCsv(ctx, user); return; }
                if (path == "/api/operation-logs" && ctx.Request.HttpMethod == "GET") { ListOperationLogs(ctx, user); return; }
                if (path.StartsWith("/api/operation-logs/") && ctx.Request.HttpMethod == "GET") { GetOperationLogDetail(ctx, user, path.Substring("/api/operation-logs/".Length)); return; }
                if (path == "/api/test-data/export-all" && ctx.Request.HttpMethod == "GET") { if (!RequireTestDataAccess(ctx, user)) return; ExportTestDataAll(ctx, user); return; }
                if (path == "/api/test-data/import-preview" && ctx.Request.HttpMethod == "POST") { if (!RequireTestDataAccess(ctx, user)) return; ImportTestDataPreview(ctx, user); return; }
                if (path == "/api/test-data/import-run" && ctx.Request.HttpMethod == "POST") { if (!RequireTestDataAccess(ctx, user)) return; ImportTestDataRun(ctx, user); return; }
                if (path == "/api/operation-impact/preview" && ctx.Request.HttpMethod == "POST") { PreviewOperationImpact(ctx, user); return; }
                if (path == "/api/operation-impact/log" && ctx.Request.HttpMethod == "POST") { LogOperationImpactCancel(ctx, user); return; }
                WriteJson(ctx, new { error = "接口不存在" }, 404);
            }
            catch (ImpactBusinessException ex)
            {
                try { LogOperationFailure(ctx, Authenticate(ctx), ex.Message, ex.StatusCode); WriteJson(ctx, new { message = ex.Message, error = ex.Message, blockingReasons = ex.BlockingReasons, impactItems = ex.ImpactItems }, ex.StatusCode); } catch { }
            }
            catch (BusinessException ex)
            {
                try { LogOperationFailure(ctx, Authenticate(ctx), ex.Message, ex.StatusCode); WriteJson(ctx, new { message = ex.Message, error = ex.Message }, ex.StatusCode); } catch { }
            }
            catch (EditConflictException ex)
            {
                try { LogOperationFailure(ctx, Authenticate(ctx), ex.Message, 409); WriteJson(ctx, new { error = "conflict", message = ex.Message }, 409); } catch { }
            }
            catch (Exception ex)
            {
                try { string msg = ToUserMessage(ex); LogOperationFailure(ctx, Authenticate(ctx), msg, 500); WriteJson(ctx, new { error = msg, message = msg }, 500); } catch { }
            }
            finally { EndAuditContext(); }
        }

        static string AppendHtmlBeforeLastBodyClose(string html, string fragment)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(fragment)) return html;
            int idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return html + fragment;
            return html.Substring(0, idx) + fragment + html.Substring(idx);
        }

        static void ServeApp(HttpListenerContext ctx)
        {
            string html;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.App.html"))
            {
                if (s == null) throw new Exception("界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = reader.ReadToEnd();
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Finance.html"))
            {
                if (s == null) throw new Exception("财务界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Customer.html"))
            {
                if (s == null) throw new Exception("客户界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Material.html"))
            {
                if (s == null) throw new Exception("物料界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Contract.html"))
            {
                if (s == null) throw new Exception("合同界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Business.html"))
            {
                if (s == null) throw new Exception("业务界面资源缺失");
                using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.TestData.html"))
            {
                if (s != null) using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.Reconciliation.html"))
            {
                if (s != null) using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupplierErpApp.OperationLog.html"))
            {
                if (s != null) using (var reader = new StreamReader(s, Encoding.UTF8)) html = AppendHtmlBeforeLastBodyClose(html, reader.ReadToEnd());
            }
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static void Login(HttpListenerContext ctx)
        {
            var req = Json.Deserialize<LoginRequest>(ReadBody(ctx.Request));
            var user = FindUser(req == null ? "" : req.Username);
            if (user == null || !FixedEquals(user.PasswordHash, Sha256(req == null ? "" : req.Password)))
            {
                Audit(null, "登录失败", req == null ? "" : req.Username);
                WriteJson(ctx, new { error = "账号或密码不正确" }, 401); return;
            }
            if (!user.Enabled)
            {
                Audit(null, "登录失败", user.Username + "（已禁用）");
                WriteJson(ctx, new { error = "账号已禁用" }, 403); return;
            }
            string token = Guid.NewGuid().ToString("N");
            var session = ToSession(user);
            lock (SessionLock) Sessions[token] = session;
            var cookie = new Cookie("ERPSESSION", token, "/"); cookie.HttpOnly = true; ctx.Response.Cookies.Add(cookie);
            Audit(session, "登录", "成功");
            WriteJson(ctx, session);
        }

        static void Logout(HttpListenerContext ctx)
        {
            var user = Authenticate(ctx);
            var c = ctx.Request.Cookies["ERPSESSION"];
            if (c != null) lock (SessionLock) Sessions.Remove(c.Value);
            if (user != null) Audit(user, "退出", "");
            var expired = new Cookie("ERPSESSION", "", "/"); expired.Expires = DateTime.Now.AddDays(-1); ctx.Response.Cookies.Add(expired);
            WriteJson(ctx, new { ok = true });
        }

        static UserSession Authenticate(HttpListenerContext ctx)
        {
            var c = ctx.Request.Cookies["ERPSESSION"];
            if (c == null || string.IsNullOrEmpty(c.Value)) return null;
            lock (SessionLock)
            {
                UserSession s;
                if (!Sessions.TryGetValue(c.Value, out s)) return null;
                var user = FindUser(s.Username);
                if (user == null || !user.Enabled) return null;
                return ToSession(user);
            }
        }

        static void EnsureUsersFile()
        {
            if (File.Exists(UsersFile)) return;
            var admin = new List<UserDef>
            {
                new UserDef
                {
                    Username = AdminUsername,
                    DisplayName = "系统管理员",
                    Role = "系统管理员",
                    PasswordHash = DefaultAdminPasswordHash,
                    Enabled = true,
                    Permissions = new string[0]
                }
            };
            File.WriteAllText(UsersFile, Json.Serialize(admin), new UTF8Encoding(false));
        }

        static void LoadUsers()
        {
            lock (DataLock)
            {
                if (!File.Exists(UsersFile)) { Users = new List<UserDef>(); return; }
                string text = File.ReadAllText(UsersFile, Encoding.UTF8);
                Users = Json.Deserialize<List<UserDef>>(text) ?? new List<UserDef>();
            }
        }

        static void SaveUsers()
        {
            lock (DataLock)
            {
                string temp = UsersFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(Users), new UTF8Encoding(false));
                if (File.Exists(UsersFile)) File.Replace(temp, UsersFile, Path.Combine(BackupDir, "users_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"));
                else File.Move(temp, UsersFile);
                CleanBackups();
            }
        }

        static UserDef FindUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            return Users.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        static bool IsAdminUser(UserSession user) { return user != null && user.IsAdmin; }
        static bool IsAdminUsername(string username) { return string.Equals(username, AdminUsername, StringComparison.OrdinalIgnoreCase); }

        static UserSession ToSession(UserDef user)
        {
            bool isAdmin = IsAdminUsername(user.Username);
            return new UserSession
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsAdmin = isAdmin,
                Permissions = isAdmin ? AllPermissionKeys : NormalizePermissions(user.Permissions)
            };
        }

        static bool CanReadTaxRate(UserSession user)
        {
            return HasPermission(user, "settings.tax_rate") || HasPermission(user, "bom.view") || HasPermission(user, "model_cost.view");
        }

        static string NormalizePriceType(string priceType)
        {
            return string.Equals(priceType, "含税", StringComparison.OrdinalIgnoreCase) ? "含税" : "不含税";
        }

        static decimal GetMaterialTaxPrice(Material material)
        {
            if (material == null) return 0;
            return material.TaxPrice;
        }

        static decimal GetMaterialNoTaxPrice(Material material)
        {
            if (material == null) return 0;
            return material.NoTaxPrice;
        }

        /// <summary>BOM 成本单价：优先含税价，无含税价时回退不含税价。</summary>
        static (decimal UnitPrice, string PriceType, bool UsedNoTaxFallback) GetMaterialBomUnitPrice(Material material)
        {
            if (material == null) return (0, "不含税", false);
            if (material.TaxPrice > 0) return (material.TaxPrice, "含税", false);
            if (material.NoTaxPrice > 0) return (material.NoTaxPrice, "不含税", true);
            return (0, "不含税", false);
        }

        static decimal CalcNoTaxUnitPrice(decimal taxUnitPrice, decimal taxRate)
        {
            if (taxUnitPrice <= 0) return 0;
            if (taxRate < 0) taxRate = 0;
            return Math.Round(taxUnitPrice / (1 + taxRate / 100m), 4);
        }

        static decimal CalcLineAmount(decimal quantity, decimal unitPrice)
        {
            return Math.Round(quantity * unitPrice, 2);
        }

        static decimal GetBomLineCostUnitPrice(BomDetail line)
        {
            if (line == null) return 0;
            if (line.NoTaxPrice > 0) return line.NoTaxPrice;
            if (line.OriginalPrice <= 0) return 0;
            if (NormalizePriceType(line.PriceType) == "含税")
            {
                decimal rate = line.TaxRate > 0 ? line.TaxRate : LoadSystemSettings().TaxRate;
                return CalcNoTaxUnitPrice(line.OriginalPrice, rate);
            }
            return line.OriginalPrice;
        }

        static void RecalcBomLines(BomItem item, decimal defaultTaxRate)
        {
            if (item.Items == null) item.Items = new List<BomDetail>();
            decimal total = 0;
            foreach (var line in item.Items)
            {
                line.PriceType = NormalizePriceType(line.PriceType);
                if (line.TaxRate <= 0) line.TaxRate = defaultTaxRate;
                if (line.OriginalPrice <= 0 && line.NoTaxPrice > 0 && line.PriceType == "不含税")
                    line.OriginalPrice = line.NoTaxPrice;
                if (line.NoTaxPrice <= 0 && line.OriginalPrice > 0)
                    line.NoTaxPrice = GetBomLineCostUnitPrice(line);
                line.Amount = CalcLineAmount(line.Quantity, GetBomLineCostUnitPrice(line));
                total += line.Amount;
            }
            item.TotalMaterialCost = Math.Round(total, 2);
        }

        static void ValidateBom(BomItem item)
        {
            if (item == null) throw new Exception("BOM 资料不能为空");
            if (string.IsNullOrWhiteSpace(item.ModelCode)) throw new Exception("机型编号不能为空");
            if (string.IsNullOrWhiteSpace(item.ModelName)) throw new Exception("机型名称不能为空");
            if (string.IsNullOrWhiteSpace(item.ProductName)) throw new Exception("产品名称不能为空");
            item.ModelCode = item.ModelCode.Trim();
            item.ModelName = item.ModelName.Trim();
            item.ProductName = item.ProductName.Trim();
            item.Version = (item.Version ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Items == null || item.Items.Count == 0) throw new Exception("请至少添加一条 BOM 明细");
            foreach (var line in item.Items)
            {
                if (string.IsNullOrWhiteSpace(line.MaterialCode)) throw new Exception("BOM 明细必须选择物料");
                if (line.Quantity <= 0) throw new Exception("BOM 明细用量必须大于零");
                line.MaterialName = (line.MaterialName ?? "").Trim();
                line.Spec = (line.Spec ?? "").Trim();
                line.Unit = (line.Unit ?? "").Trim();
                line.Note = (line.Note ?? "").Trim();
                line.PriceSourceTime = (line.PriceSourceTime ?? "").Trim();
                if (string.IsNullOrWhiteSpace(line.PriceSourceTime))
                    line.PriceSourceTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        static void ValidateModelCost(ModelCost item)
        {
            if (item == null) throw new Exception("机型成本资料不能为空");
            if (string.IsNullOrWhiteSpace(item.BomId)) throw new Exception("请选择 BOM");
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Status != "启用" && item.Status != "停用") throw new Exception("状态只能是启用或停用");
        }

        static bool IsMaterialUsedByBom(string materialId, string materialCode)
        {
            return GetBomsUsingMaterial(materialId, materialCode).Count > 0;
        }

        static List<BomItem> GetBomsUsingMaterial(string materialId, string materialCode)
        {
            return LoadBom().Where(b => (b.Items ?? new List<BomDetail>()).Any(x => x.MaterialId == materialId || (!string.IsNullOrWhiteSpace(materialCode) && x.MaterialCode == materialCode))).ToList();
        }

        static string FormatMaterialDeleteBlockedMessage(List<BomItem> boms)
        {
            string msg = "该物料已被 BOM 使用，不能删除。请先删除相关 BOM 后再删除该物料。";
            if (boms == null || boms.Count == 0) return msg;
            var refs = boms.Take(10).Select(b => (string.IsNullOrWhiteSpace(b.Code) ? "" : b.Code) + " " + b.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0);
            msg += " 引用 BOM：" + string.Join("、", refs);
            if (boms.Count > 10) msg += " 等共" + boms.Count + "条";
            return msg;
        }

        static bool IsBomUsedByModelCost(string bomId)
        {
            return GetModelCostsUsingBom(bomId).Count > 0;
        }

        static List<ModelCost> GetModelCostsUsingBom(string bomId)
        {
            return LoadModelCosts().Where(x => x.BomId == bomId).ToList();
        }

        static string FormatBomDeleteBlockedMessage(List<ModelCost> costs)
        {
            string msg = "该 BOM 已被机型成本使用，不能删除。请先删除相关机型成本后再删除该 BOM。";
            if (costs == null || costs.Count == 0) return msg;
            var refs = costs.Take(10).Select(x => (string.IsNullOrWhiteSpace(x.ModelCode) ? "" : x.ModelCode) + " " + x.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0);
            msg += " 引用机型成本：" + string.Join("、", refs);
            if (costs.Count > 10) msg += " 等共" + costs.Count + "条";
            return msg;
        }

        static bool IsSupplierUsedByMaterial(string company)
        {
            if (string.IsNullOrWhiteSpace(company)) return false;
            return LoadMaterials().Any(x => string.Equals(x.Supplier, company, StringComparison.OrdinalIgnoreCase));
        }

        static void ValidateModelCostFilled(ModelCost item)
        {
            if (string.IsNullOrWhiteSpace(item.ModelCode)) throw new Exception("机型编号不能为空");
            item.ModelName = (item.ModelName ?? "").Trim();
            item.ProductName = (item.ProductName ?? "").Trim();
            item.BomVersion = (item.BomVersion ?? "").Trim();
            item.BomCode = (item.BomCode ?? "").Trim();
        }

        static SystemSettings LoadSystemSettings()
        {
            lock (DataLock)
            {
                if (!File.Exists(SystemSettingsFile)) return new SystemSettings { TaxRate = 10, ClearDataPassword = DefaultClearDataPassword };
                string text = File.ReadAllText(SystemSettingsFile, Encoding.UTF8);
                var settings = Json.Deserialize<SystemSettings>(text) ?? new SystemSettings();
                if (settings.TaxRate < 0) settings.TaxRate = 0;
                if (string.IsNullOrWhiteSpace(settings.ClearDataPassword)) settings.ClearDataPassword = DefaultClearDataPassword;
                return settings;
            }
        }

        static void EnsureSystemSettingsDefaults()
        {
            lock (DataLock)
            {
                if (!File.Exists(SystemSettingsFile))
                {
                    SaveSystemSettingsFile(new SystemSettings { TaxRate = 10, ClearDataPassword = DefaultClearDataPassword });
                    return;
                }
                var settings = Json.Deserialize<SystemSettings>(File.ReadAllText(SystemSettingsFile, Encoding.UTF8)) ?? new SystemSettings();
                if (string.IsNullOrWhiteSpace(settings.ClearDataPassword))
                {
                    settings.ClearDataPassword = DefaultClearDataPassword;
                    SaveSystemSettingsFile(settings);
                }
            }
        }

        static void SaveSystemSettingsFile(SystemSettings settings)
        {
            lock (DataLock)
            {
                string temp = SystemSettingsFile + ".tmp";
                File.WriteAllText(temp, Json.Serialize(settings), new UTF8Encoding(false));
                if (File.Exists(SystemSettingsFile))
                {
                    string backup = Path.Combine(BackupDir, "system_settings_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                    File.Replace(temp, SystemSettingsFile, backup);
                }
                else File.Move(temp, SystemSettingsFile);
                CleanBackups();
            }
        }

        static void SaveTaxRate(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<TaxRateRequest>(ReadBody(ctx.Request));
            if (req == null) { WriteJson(ctx, new { error = "请求无效" }, 400); return; }
            if (req.TaxRate < 0) { WriteJson(ctx, new { error = "税率不能小于 0" }, 400); return; }
            SystemSettings saved = null;
            RunUnderDataLock(() =>
            {
                var settings = LoadSystemSettings();
                EnsureEditVersionMatch(settings.UpdatedAt, req.UpdatedAt);
                settings.TaxRate = req.TaxRate;
                settings.UpdatedAt = ProfileUpdatedAtNow();
                SaveSystemSettingsFile(settings);
                saved = settings;
            });
            Audit(user, "修改税率", saved.TaxRate.ToString("0.##") + "%");
            WriteJson(ctx, saved);
        }

        static List<BomItem> LoadBom()
        {
            lock (DataLock) return ReadJsonListCore<BomItem>(BomFile);
        }

        static void ApplyCurrentMaterialPrices(BomItem item)
        {
            if (item == null) return;
            var materials = LoadMaterials();
            decimal taxRate = LoadSystemSettings().TaxRate, total = 0;
            var missing = new List<string>();
            foreach (var line in item.Items ?? new List<BomDetail>())
            {
                Material material = null;
                if (!string.IsNullOrWhiteSpace(line.MaterialId))
                    material = materials.FirstOrDefault(x => x.Id == line.MaterialId);
                if (material == null && !string.IsNullOrWhiteSpace(line.MaterialCode))
                    material = materials.FirstOrDefault(x => x.Code == line.MaterialCode);
                if (material == null && !string.IsNullOrWhiteSpace(line.MaterialName))
                    material = materials.FirstOrDefault(x => string.Equals(x.NameSpec, line.MaterialName, StringComparison.OrdinalIgnoreCase));
                if (material != null)
                {
                    var price = GetMaterialBomUnitPrice(material);
                    line.MaterialId = material.Id;
                    line.MaterialCode = material.Code;
                    line.MaterialName = material.NameSpec;
                    line.Unit = string.IsNullOrWhiteSpace(material.QuantityUnit) ? line.Unit : material.QuantityUnit;
                    line.PriceType = price.PriceType;
                    line.TaxRate = taxRate;
                    line.PriceSourceTime = material.UpdatedAt;
                    line.PriceMissing = price.UnitPrice <= 0;
                    if (price.PriceType == "含税")
                    {
                        line.OriginalPrice = price.UnitPrice;
                        line.NoTaxPrice = CalcNoTaxUnitPrice(price.UnitPrice, taxRate);
                    }
                    else
                    {
                        line.OriginalPrice = price.UnitPrice;
                        line.NoTaxPrice = price.UnitPrice;
                    }
                }
                else
                {
                    line.PriceMissing = true;
                }
                if (line.PriceMissing)
                    missing.Add(string.IsNullOrWhiteSpace(line.MaterialName) ? (line.MaterialCode ?? "未命名物料") : line.MaterialName);
                if (line.OriginalPrice <= 0 && line.NoTaxPrice > 0)
                {
                    line.OriginalPrice = line.NoTaxPrice;
                    line.PriceType = "不含税";
                }
                if (line.NoTaxPrice <= 0 && line.OriginalPrice > 0)
                    line.NoTaxPrice = GetBomLineCostUnitPrice(line);
                line.Amount = CalcLineAmount(line.Quantity, GetBomLineCostUnitPrice(line));
                total += line.Amount;
            }
            item.TotalMaterialCost = Math.Round(total, 2);
            item.PriceMissing = missing.Count > 0;
            item.MissingPriceMaterials = string.Join("、", missing.Distinct());
        }

        static List<BomItem> LoadBomWithCurrentPrices()
        {
            var list = LoadBom();
            foreach (var item in list) ApplyCurrentMaterialPrices(item);
            return list;
        }

        static void SaveBom(List<BomItem> items)
        {
            lock (DataLock) WriteJsonListCore(BomFile, "bom", items);
        }

        static void EnsureModelCode(BomItem item)
        {
            if (item == null) return;
            if (!string.IsNullOrWhiteSpace(item.ModelCode) && item.ModelCode.Trim() != "-") return;
            var codes = LoadBom().Select(x => x.ModelCode).Concat(LoadModelCosts().Select(x => x.ModelCode));
            item.ModelCode = NextCode(ModelCostSequenceFile, "MC", codes);
        }

        static void AddBom(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<BomItem>(ReadBody(ctx.Request));
            EnsureModelCode(item);
            ValidateBom(item);
            ApplyCurrentMaterialPrices(item);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var saved = MutateJsonList<BomItem, BomItem>(BomFile, "bom", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(BomSequenceFile, "BOM", list.Select(x => x.Code));
                item.CreatedAt = now;
                item.UpdatedAt = now;
                list.Insert(0, item);
                return new JsonMutationResult<BomItem>(item, true);
            });
            Audit(user, "新增BOM", saved.Code + " " + saved.ModelName);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateBom(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<BomItem>(ReadBody(ctx.Request));
            ValidateBom(input);
            var saved = MutateJsonList<BomItem, BomItem>(BomFile, "bom", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("BOM 不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureBomReferenceLockForEdit(id, item, input);
                if (!IsBomUsedByModelCost(id)) ApplyCurrentMaterialPrices(input);
                input.Id = item.Id;
                input.Code = item.Code;
                input.CreatedAt = item.CreatedAt;
                input.UpdatedAt = BizUpdatedAtNow();
                list[list.IndexOf(item)] = input;
                return new JsonMutationResult<BomItem>(input, true);
            });
            Audit(user, "修改BOM", saved.Code + " " + saved.ModelName);
            WriteJson(ctx, saved);
        }

        static void DeleteBom(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<BomItem>(BomFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("BOM 不存在", 404);
                auditDetail = item.Code + " " + item.ModelName;
            });
            EnforceDeleteImpact("bom", id, user, ctx, auditDetail);
            MutateJsonList<BomItem, object>(BomFile, "bom", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("BOM 不存在", 404);
                if (IsBomUsedByModelCost(id)) throw new BusinessException(FormatBomDeleteBlockedMessage(GetModelCostsUsingBom(id)), 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除BOM", auditDetail);
            WriteJson(ctx, new { ok = true });
        }

        static List<ModelCost> LoadModelCosts()
        {
            lock (DataLock) return ReadJsonListCore<ModelCost>(ModelCostFile);
        }

        static List<ModelCost> LoadModelCostsWithCurrentPrices()
        {
            var list = LoadModelCosts();
            var boms = LoadBom();
            foreach (var item in list)
            {
                var bom = boms.FirstOrDefault(x => x.Id == item.BomId);
                if (bom == null) continue;
                item.PriceMissing = bom.PriceMissing;
                item.MissingPriceMaterials = bom.MissingPriceMaterials;
                if (item.MaterialCost <= 0 && bom.TotalMaterialCost > 0)
                {
                    item.MaterialCost = bom.TotalMaterialCost;
                    item.TotalCost = item.MaterialCost;
                }
            }
            return list;
        }

        static void SaveModelCosts(List<ModelCost> items)
        {
            lock (DataLock) WriteJsonListCore(ModelCostFile, "model_costs", items);
        }

        static void FillModelCostFromBom(ModelCost item, bool allowDisabledBom = false, bool refreshPrices = false)
        {
            var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
            if (bom == null) throw new Exception("所选 BOM 不存在");
            if (!allowDisabledBom && (bom.Status ?? "启用") != "启用") throw new Exception("所选 BOM 已停用，不能新建或更换为该 BOM");
            if (refreshPrices) ApplyCurrentMaterialPrices(bom);
            item.ModelCode = bom.ModelCode;
            item.ModelName = bom.ModelName;
            item.ProductName = bom.ProductName;
            item.BomVersion = bom.Version;
            item.BomCode = bom.Code;
            item.MaterialCost = bom.TotalMaterialCost;
            item.TotalCost = item.MaterialCost;
            item.PriceMissing = bom.PriceMissing;
            item.MissingPriceMaterials = bom.MissingPriceMaterials;
        }

        static void AddModelCost(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ModelCost>(ReadBody(ctx.Request));
            ValidateModelCost(item);
            FillModelCostFromBom(item, false, false);
            ValidateModelCostFilled(item);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "启用";
            var saved = MutateJsonList<ModelCost, ModelCost>(ModelCostFile, "model_costs", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.CreatedAt = now;
                item.UpdatedAt = now;
                list.Insert(0, item);
                return new JsonMutationResult<ModelCost>(item, true);
            });
            Audit(user, "新增机型成本", saved.ModelCode + " " + saved.ModelName);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateModelCost(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ModelCost>(ReadBody(ctx.Request));
            ValidateModelCost(input);
            var listSnapshot = LoadModelCosts();
            var existing = listSnapshot.FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "机型成本记录不存在" }, 404); return; }
            bool sameBom = string.Equals(existing.BomId, input.BomId, StringComparison.OrdinalIgnoreCase);
            FillModelCostFromBom(input, sameBom, !sameBom);
            if (sameBom)
            {
                input.MaterialCost = existing.MaterialCost;
                input.TotalCost = existing.TotalCost;
            }
            ValidateModelCostFilled(input);
            input.Status = string.IsNullOrWhiteSpace(input.Status) ? (existing.Status ?? "启用") : input.Status.Trim();
            if (input.Status != "启用" && input.Status != "停用") { WriteJson(ctx, new { error = "状态只能是启用或停用" }, 400); return; }
            if (input.Status == "启用")
            {
                var bomCheck = LoadBom().FirstOrDefault(x => x.Id == input.BomId);
                if (bomCheck != null && (bomCheck.Status ?? "启用") != "启用") { WriteJson(ctx, new { error = "该机型成本关联的 BOM 已停用，不能启用。" }, 409); return; }
            }
            var saved = MutateJsonList<ModelCost, ModelCost>(ModelCostFile, "model_costs", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("机型成本记录不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                input.Id = item.Id;
                input.CreatedAt = item.CreatedAt;
                input.UpdatedAt = BizUpdatedAtNow();
                list[list.IndexOf(item)] = input;
                return new JsonMutationResult<ModelCost>(input, true);
            });
            Audit(user, "修改机型成本", saved.ModelCode + " " + saved.ModelName);
            WriteJson(ctx, saved);
        }

        static void DeleteModelCost(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<ModelCost>(ModelCostFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("机型成本记录不存在", 404);
                auditDetail = item.ModelCode + " " + item.ModelName;
            });
            EnforceDeleteImpact("modelCost", id, user, ctx, auditDetail);
            MutateJsonList<ModelCost, object>(ModelCostFile, "model_costs", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("机型成本记录不存在", 404);
                if ((item.Status ?? "启用") == "启用") throw new BusinessException("该机型成本当前为启用状态，不能删除。请先停用后再删除。", 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除机型成本", auditDetail);
            WriteJson(ctx, new { ok = true });
        }

        static string GetQueryParam(HttpListenerContext ctx, string key)
        {
            string q = ctx.Request.Url.Query;
            if (string.IsNullOrEmpty(q)) return "";
            foreach (var part in q.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && string.Equals(Uri.UnescapeDataString(kv[0]), key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return "";
        }

        static readonly string[] DictionaryCategories = new[] {
            "SupplierType", "CustomerType", "MaterialCategory", "MaterialUnit", "FinanceItem", "ContractType", "OrderCategory", "OtherCategory", "BomStatus", "ModelCostStatus"
        };

        static List<DictionaryOption> LoadDictionaryOptions()
        {
            lock (DataLock) return ReadJsonListCore<DictionaryOption>(DictionaryOptionsFile);
        }

        static void SaveDictionaryOptions(List<DictionaryOption> items)
        {
            lock (DataLock) WriteJsonListCore(DictionaryOptionsFile, "dictionary_options", items);
        }

        static void EnsureDefaultDictionaryOptions()
        {
            lock (DataLock)
            {
                var list = File.Exists(DictionaryOptionsFile) ? (Json.Deserialize<List<DictionaryOption>>(File.ReadAllText(DictionaryOptionsFile, Encoding.UTF8)) ?? new List<DictionaryOption>()) : new List<DictionaryOption>();
                if (list.Count > 0) return;
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var defaults = new List<Tuple<string, string, int>>
                {
                    Tuple.Create("SupplierType", "原材料供应商", 1),
                    Tuple.Create("SupplierType", "外协加工厂", 2),
                    Tuple.Create("SupplierType", "设备供应商", 3),
                    Tuple.Create("CustomerType", "终端客户", 1),
                    Tuple.Create("CustomerType", "经销商", 2),
                    Tuple.Create("CustomerType", "代理商", 3),
                    Tuple.Create("MaterialCategory", "原材料", 1),
                    Tuple.Create("MaterialCategory", "半成品", 2),
                    Tuple.Create("MaterialCategory", "辅料", 3),
                    Tuple.Create("MaterialCategory", "包材", 4),
                    Tuple.Create("MaterialUnit", "个", 1),
                    Tuple.Create("MaterialUnit", "件", 2),
                    Tuple.Create("MaterialUnit", "套", 3),
                    Tuple.Create("MaterialUnit", "kg", 4),
                    Tuple.Create("MaterialUnit", "m", 5),
                    Tuple.Create("FinanceItem", "销售收入", 1),
                    Tuple.Create("FinanceItem", "采购支出", 2),
                    Tuple.Create("FinanceItem", "办公费用", 3),
                    Tuple.Create("FinanceItem", "差旅费", 4),
                    Tuple.Create("ContractType", "设备购销合同", 1),
                    Tuple.Create("ContractType", "配件购销合同", 2),
                    Tuple.Create("ContractType", "维保合同", 3),
                    Tuple.Create("BomStatus", "启用", 1),
                    Tuple.Create("BomStatus", "停用", 2),
                    Tuple.Create("ModelCostStatus", "启用", 1),
                    Tuple.Create("ModelCostStatus", "停用", 2)
                };
                foreach (var d in defaults)
                {
                    list.Add(new DictionaryOption
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Category = d.Item1,
                        Name = d.Item2,
                        Status = "启用",
                        SortOrder = d.Item3,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                SaveDictionaryOptions(list);
            }
        }

        static void ValidateDictionaryOption(DictionaryOption item)
        {
            if (item == null) throw new Exception("字典项不能为空");
            if (string.IsNullOrWhiteSpace(item.Category)) throw new Exception("字典分类不能为空");
            if (Array.IndexOf(DictionaryCategories, item.Category.Trim()) < 0) throw new Exception("字典分类无效");
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("字典名称不能为空");
            item.Category = item.Category.Trim();
            item.Name = item.Name.Trim();
            item.Value = string.IsNullOrWhiteSpace(item.Value) ? item.Name : item.Value.Trim();
            item.Note = (item.Note ?? "").Trim();
            item.Status = string.IsNullOrWhiteSpace(item.Status) ? "启用" : item.Status.Trim();
            if (item.Status != "启用" && item.Status != "停用") throw new Exception("状态只能是启用或停用");
        }

        static bool IsDictionaryOptionInUse(DictionaryOption item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name)) return false;
            string name = item.Name;
            switch (item.Category)
            {
                case "MaterialUnit":
                    return LoadMaterials().Any(x => string.Equals(x.QuantityUnit, name, StringComparison.OrdinalIgnoreCase));
                case "FinanceItem":
                    return LoadFinance().Any(x => string.Equals(x.Purpose, name, StringComparison.OrdinalIgnoreCase) || string.Equals(x.AccountType, name, StringComparison.OrdinalIgnoreCase));
                case "BomStatus":
                    return LoadBom().Any(x => string.Equals(x.Status ?? "启用", name, StringComparison.OrdinalIgnoreCase));
                case "ModelCostStatus":
                    return LoadModelCosts().Any(x => string.Equals(x.Status ?? "启用", name, StringComparison.OrdinalIgnoreCase));
                default:
                    return false;
            }
        }

        static void ListDictionaryOptions(HttpListenerContext ctx)
        {
            string category = GetQueryParam(ctx, "category");
            var list = LoadDictionaryOptions();
            if (!string.IsNullOrWhiteSpace(category))
                list = list.Where(x => string.Equals(x.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            list = list.OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
            WriteJson(ctx, list);
        }

        static void AddDictionaryOption(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<DictionaryOption>(ReadBody(ctx.Request));
            ValidateDictionaryOption(item);
            string now = ProfileUpdatedAtNow();
            var saved = MutateJsonList<DictionaryOption, DictionaryOption>(DictionaryOptionsFile, "dictionary_options", list =>
            {
                if (list.Any(x => string.Equals(x.Category, item.Category, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该分类下已存在相同名称的字典项", 409);
                item.Id = Guid.NewGuid().ToString("N");
                if (item.SortOrder <= 0) item.SortOrder = list.Where(x => x.Category == item.Category).Select(x => x.SortOrder).DefaultIfEmpty(0).Max() + 1;
                item.CreatedAt = now;
                item.UpdatedAt = now;
                list.Add(item);
                return new JsonMutationResult<DictionaryOption>(item, true);
            });
            Audit(user, "新增字典项", saved.Category + " " + saved.Name);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateDictionaryOption(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<DictionaryOption>(ReadBody(ctx.Request));
            ValidateDictionaryOption(input);
            var saved = MutateJsonList<DictionaryOption, DictionaryOption>(DictionaryOptionsFile, "dictionary_options", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("字典项不存在", 404);
                if (list.Any(x => x.Id != id && string.Equals(x.Category, input.Category, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, input.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该分类下已存在相同名称的字典项", 409);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                item.Category = input.Category;
                item.Name = input.Name;
                item.Value = string.IsNullOrWhiteSpace(input.Value) ? input.Name : input.Value.Trim();
                item.Note = (input.Note ?? "").Trim();
                item.Status = input.Status;
                if (input.SortOrder > 0) item.SortOrder = input.SortOrder;
                item.UpdatedAt = ProfileUpdatedAtNow();
                return new JsonMutationResult<DictionaryOption>(item, true);
            });
            Audit(user, "修改字典项", saved.Category + " " + saved.Name);
            WriteJson(ctx, saved);
        }

        static void DeleteDictionaryOption(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<DictionaryOption>(DictionaryOptionsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("字典项不存在", 404);
                auditDetail = item.Category + " " + item.Name;
            });
            EnforceDeleteImpact("dictionary", id, user, ctx, auditDetail);
            MutateJsonList<DictionaryOption, object>(DictionaryOptionsFile, "dictionary_options", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("字典项不存在", 404);
                if (IsDictionaryOptionInUse(item)) throw new BusinessException("该字典项已被业务数据使用，不能删除。请改为停用。", 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除字典项", auditDetail);
            WriteJson(ctx, new { ok = true });
        }

        static string BuildPermissionSummary(UserDef user)
        {
            if (IsAdminUsername(user.Username)) return "全部权限";
            var perms = NormalizePermissions(user.Permissions);
            if (perms.Length == 0) return "无权限";
            if (perms.Length == AllPermissionKeys.Length) return "全部权限";
            var labels = new Dictionary<string, string>();
            foreach (var g in GetPermissionDefinitions())
                foreach (var item in g.Items) labels[item.Key] = g.Module + "·" + item.Label;
            return string.Join("、", perms.Take(6).Select(p => labels.ContainsKey(p) ? labels[p] : p)) + (perms.Length > 6 ? "…" : "");
        }

        static UserPublic ToPublic(UserDef user)
        {
            return new UserPublic
            {
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                Enabled = user.Enabled,
                Permissions = IsAdminUsername(user.Username) ? AllPermissionKeys : NormalizePermissions(user.Permissions),
                PermissionSummary = BuildPermissionSummary(user),
                UpdatedAt = user.UpdatedAt
            };
        }

        static void InvalidateUserSessions(string username)
        {
            lock (SessionLock)
            {
                foreach (var key in Sessions.Where(x => string.Equals(x.Value.Username, username, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToList())
                    Sessions.Remove(key);
            }
        }

        static void ListUsers(HttpListenerContext ctx)
        {
            WriteJson(ctx, Users.Select(ToPublic).OrderBy(x => x.Username).ToList());
        }

        static void CreateUser(HttpListenerContext ctx, UserSession actor)
        {
            var req = Json.Deserialize<CreateUserRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Username)) { WriteJson(ctx, new { error = "账户名不能为空" }, 400); return; }
            if (string.IsNullOrWhiteSpace(req.Password)) { WriteJson(ctx, new { error = "密码不能为空" }, 400); return; }
            string username = req.Username.Trim();
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能创建同名主账号" }, 409); return; }
            UserPublic saved = null;
            RunUnderDataLock(() =>
            {
                if (FindUser(username) != null) throw new BusinessException("账户名已存在", 409);
                var user = new UserDef
                {
                    Username = username,
                    DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? username : req.DisplayName.Trim(),
                    Role = "普通用户",
                    PasswordHash = Sha256(req.Password),
                    Enabled = req.Enabled,
                    Permissions = NormalizePermissions(req.Permissions),
                    UpdatedAt = ProfileUpdatedAtNow()
                };
                Users.Add(user);
                SaveUsers();
                saved = ToPublic(user);
            });
            Audit(actor, "新增子账号", saved.Username);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateUser(HttpListenerContext ctx, UserSession actor, string username)
        {
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能修改主账号权限或状态" }, 403); return; }
            var req = Json.Deserialize<UpdateUserRequest>(ReadBody(ctx.Request));
            if (req == null) { WriteJson(ctx, new { error = "请求无效" }, 400); return; }
            UserPublic saved = null;
            string sessionUser = null;
            RunUnderDataLock(() =>
            {
                var user = FindUser(username);
                if (user == null) throw new BusinessException("账号不存在", 404);
                EnsureEditVersionMatch(user.UpdatedAt, req.UpdatedAt);
                if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName.Trim();
                user.Enabled = req.Enabled;
                user.Permissions = NormalizePermissions(req.Permissions);
                if (!string.IsNullOrWhiteSpace(req.Password)) user.PasswordHash = Sha256(req.Password);
                user.UpdatedAt = ProfileUpdatedAtNow();
                SaveUsers();
                sessionUser = user.Username;
                saved = ToPublic(user);
            });
            InvalidateUserSessions(sessionUser);
            Audit(actor, "编辑子账号", saved.Username);
            WriteJson(ctx, saved);
        }

        static void DeleteUser(HttpListenerContext ctx, UserSession actor, string username)
        {
            EnforceDeleteImpact("user", username, actor, ctx, username);
            if (IsAdminUsername(username)) { WriteJson(ctx, new { error = "不能删除主账号" }, 403); return; }
            string deletedUser = null;
            RunUnderDataLock(() =>
            {
                var user = FindUser(username);
                if (user == null) throw new BusinessException("账号不存在", 404);
                deletedUser = user.Username;
                Users.Remove(user);
                SaveUsers();
            });
            InvalidateUserSessions(deletedUser);
            Audit(actor, "删除子账号", deletedUser);
            WriteJson(ctx, new { ok = true });
        }

        static void ChangePassword(HttpListenerContext ctx, UserSession user)
        {
            if (!HasPermission(user, "settings.password")) { WriteJson(ctx, new { error = "无权限操作" }, 403); return; }
            var req = Json.Deserialize<ChangePasswordRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.OldPassword)) { WriteJson(ctx, new { error = "请输入旧密码" }, 400); return; }
            if (string.IsNullOrWhiteSpace(req.NewPassword)) { WriteJson(ctx, new { error = "请输入新密码" }, 400); return; }
            string username = null;
            RunUnderDataLock(() =>
            {
                var def = FindUser(user.Username);
                if (def == null) throw new BusinessException("账号不存在", 404);
                if (!FixedEquals(def.PasswordHash, Sha256(req.OldPassword))) throw new BusinessException("旧密码不正确", 401);
                def.PasswordHash = Sha256(req.NewPassword);
                SaveUsers();
                username = def.Username;
            });
            InvalidateUserSessions(username);
            Audit(user, "修改密码", username);
            WriteJson(ctx, new { ok = true });
        }

        static List<Supplier> LoadSuppliers()
        {
            lock (DataLock) return ReadJsonListCore<Supplier>(DataFile);
        }

        static void SaveSuppliers(List<Supplier> items)
        {
            lock (DataLock) WriteJsonListCore(DataFile, "auto", items);
        }

        static void AddSupplier(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Supplier>(ReadBody(ctx.Request));
            Validate(item);
            var saved = MutateJsonList<Supplier, Supplier>(DataFile, "auto", list =>
            {
                if (list.Any(x => string.Equals(x.Company, item.Company, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该供应商公司已经存在", 409);
                item.Id = Guid.NewGuid().ToString("N"); item.Code = NextCode(SupplierSequenceFile, "SRM", list.Select(x => x.Code), "GY"); item.Status = "启用"; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<Supplier>(item, true);
            });
            Audit(user, "新增供应商", saved.Company); WriteJson(ctx, saved, 201);
        }

        static void EnsureEditVersionMatch(string currentUpdatedAt, string clientUpdatedAt)
        {
            string current = (currentUpdatedAt ?? "").Trim();
            string client = (clientUpdatedAt ?? "").Trim();
            if (!string.Equals(current, client, StringComparison.Ordinal))
                throw new EditConflictException();
        }

        static string ProfileUpdatedAtNow()
        {
            return DateTimeOffset.UtcNow.ToString("O");
        }

        static void UpdateSupplier(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Supplier>(ReadBody(ctx.Request)); Validate(input);
            var saved = MutateJsonList<Supplier, Supplier>(DataFile, "auto", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("供应商不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (list.Any(x => x.Id != id && string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该供应商公司已经存在", 409);
                EnsureSupplierReferenceLockForEdit(item, input);
                item.Company = input.Company; item.Contact = input.Contact; item.Phone = input.Phone; item.Goods = input.Goods; item.Address = input.Address; item.Bank = input.Bank; item.Account = input.Account; item.BankNo = input.BankNo; item.Payable = input.Payable; item.Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<Supplier>(item, true);
            });
            Audit(user, "修改供应商", saved.Company); WriteJson(ctx, saved);
        }

        static void DeleteSupplier(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<Supplier>(DataFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("供应商不存在", 404);
                auditDetail = item.Company;
            });
            EnforceDeleteImpact("supplier", id, user, ctx, auditDetail);
            MutateJsonList<Supplier, object>(DataFile, "auto", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("供应商不存在", 404);
                if (IsSupplierReferencedByBusiness(item.Company))
                {
                    if (IsSupplierUsedByMaterial(item.Company))
                        throw new BusinessException("该供应商已被物料使用，不能删除。如需删除，请先从物料管理中移除或更换相关物料的供应商。", 409);
                    throw new BusinessException(ReferenceLockMessage, 409);
                }
                list.Remove(item);
                Audit(user, "删除供应商", item.Company);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteSuppliers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            EnforceBatchDeleteImpact("supplier", ids, user, ctx);
            var deleted = MutateJsonList<Supplier, int>(DataFile, "auto", list =>
            {
                var removed = list.Where(x => ids.Contains(x.Id)).ToList();
                if (removed.Count == 0) throw new BusinessException("未找到可删除的供应商", 404);
                if (removed.Any(item => IsSupplierUsedByMaterial(item.Company)))
                    throw new BusinessException("所选供应商中存在已被物料使用的记录，不能删除。如需删除，请先从物料管理中移除或更换相关物料的供应商。", 409);
                foreach (var item in removed) list.Remove(item);
                return new JsonMutationResult<int>(removed.Count, true);
            });
            Audit(user, "批量删除供应商", "共" + deleted + "条");
            WriteJson(ctx, new { ok = true, deleted = deleted });
        }

        static void BatchAddSuppliers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchSupplierRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条供应商资料" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<Supplier, object>(DataFile, "auto", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var batchCompanies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pending = new List<Supplier>();
                foreach (var input in items)
                {
                    rowNo++;
                    try
                    {
                        Validate(input);
                        if (list.Any(x => string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)) || batchCompanies.Contains(input.Company))
                        {
                            skipped++; errors.Add("第" + rowNo + "行：供应商已存在"); continue;
                        }
                        var item = new Supplier
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Code = NextCode(SupplierSequenceFile, "SRM", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "GY"),
                            Company = input.Company, Contact = input.Contact, Phone = input.Phone, Goods = input.Goods,
                            Address = input.Address, Bank = input.Bank, Account = input.Account, BankNo = input.BankNo,
                            Payable = input.Payable, Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                            UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                        };
                        batchCompanies.Add(item.Company);
                        pending.Insert(0, item);
                        imported++;
                    }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加供应商", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static List<Customer> LoadCustomers()
        {
            lock (DataLock) return ReadJsonListCore<Customer>(CustomerFile);
        }

        static void SaveCustomers(List<Customer> items)
        {
            lock (DataLock) WriteJsonListCore(CustomerFile, "customers", items);
        }

        static int ParseCodeSequence(string code, params string[] prefixes)
        {
            if (string.IsNullOrWhiteSpace(code) || prefixes == null || prefixes.Length == 0) return 0;
            int best = 0;
            foreach (var prefix in prefixes)
            {
                if (string.IsNullOrEmpty(prefix)) continue;
                if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    int value;
                    if (int.TryParse(code.Substring(prefix.Length), out value)) best = Math.Max(best, value);
                }
            }
            return best;
        }

        static string NextCode(string sequenceFile, string prefix, IEnumerable<string> codes, params string[] legacyPrefixes)
        {
            var allPrefixes = new List<string> { prefix };
            if (legacyPrefixes != null)
            {
                foreach (var lp in legacyPrefixes)
                {
                    if (string.IsNullOrEmpty(lp)) continue;
                    if (!allPrefixes.Any(x => string.Equals(x, lp, StringComparison.OrdinalIgnoreCase)))
                        allPrefixes.Add(lp);
                }
            }
            int max = 0;
            foreach (var code in codes.Select(x => x ?? ""))
                max = Math.Max(max, ParseCodeSequence(code, allPrefixes.ToArray()));
            lock (DataLock)
            {
                int sequence = Math.Max(ReadSequenceValue(sequenceFile), max) + 1;
                WriteAllTextAtomic(sequenceFile, sequence.ToString());
                return prefix + sequence.ToString("D5");
            }
        }

        static int ReadSequenceValue(string path)
        {
            try
            {
                if (!File.Exists(path)) return 0;
                string text = File.ReadAllText(path, Encoding.UTF8).Trim();
                int v;
                if (int.TryParse(text, out v) && v >= 0) return v;
                return 0;
            }
            catch { return 0; }
        }

        static void WriteAllTextAtomic(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string temp = path + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    string backup = path + ".bak";
                    File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                    try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                }
                else File.Move(temp, path);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        static void RepairAllSequenceFiles()
        {
            foreach (var path in new[]
            {
                SupplierSequenceFile, CustomerSequenceFile, MaterialSequenceFile, BomSequenceFile, ModelCostSequenceFile,
                ContractSequenceFile, ContractSettingSequenceFile, SalesOrderSequenceFile, SalesOutboundSequenceFile,
                PurchaseOrderSequenceFile, PurchaseInboundSequenceFile, ProductionPickSequenceFile,
                FinishedInboundSequenceFile, ReceivableSequenceFile, PayableSequenceFile
            })
            {
                int v = ReadSequenceValue(path);
                WriteAllTextAtomic(path, v.ToString());
            }
        }

        static void EnsureClearedDataIntegrity()
        {
            EnsureJsonFile(DataFile);
            EnsureJsonFile(SupplierSequenceFile, "0");
            EnsureJsonFile(CustomerFile);
            EnsureJsonFile(CustomerSequenceFile, "0");
            EnsureJsonFile(MaterialFile);
            EnsureJsonFile(MaterialSequenceFile, "0");
            EnsureJsonFile(FinanceFile);
            EnsureJsonFile(OpeningFile, Json.Serialize(new OpeningBalances()));
            EnsureJsonFile(BomFile);
            EnsureJsonFile(BomSequenceFile, "0");
            EnsureJsonFile(ModelCostFile);
            EnsureJsonFile(ModelCostSequenceFile, "0");
            EnsureJsonFile(ContractsFile);
            EnsureJsonFile(ContractSequenceFile, "0");
            EnsureBusinessDataFiles();
            RepairAllSequenceFiles();
        }

        static string ToUserMessage(Exception ex)
        {
            if (ex == null) return "操作失败";
            if (ex is UnauthorizedAccessException) return "数据文件写入失败，请确认只有一个 ERP 程序在运行，并以管理员身份启动";
            var io = ex as IOException ?? ex.InnerException as IOException;
            if (io != null)
            {
                string msg = io.Message ?? "";
                if (msg.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("being used", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "数据文件被占用或权限不足，请关闭其他 ERP 实例后重试";
            }
            return string.IsNullOrWhiteSpace(ex.Message) ? "操作失败" : ex.Message;
        }

        static void EnsureLegacyCodes()
        {
            MutateJsonList<Supplier, object>(DataFile, "auto", list =>
            {
                bool changed = false;
                foreach (var item in list.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse())
                {
                    item.Code = NextCode(SupplierSequenceFile, "SRM", list.Select(x => x.Code), "GY");
                    changed = true;
                }
                return new JsonMutationResult<object>(null, changed);
            });
            MutateJsonList<Customer, object>(CustomerFile, "customers", list =>
            {
                bool changed = false;
                foreach (var item in list.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse())
                {
                    item.Code = NextCode(CustomerSequenceFile, "CRM", list.Select(x => x.Code), "KH");
                    changed = true;
                }
                return new JsonMutationResult<object>(null, changed);
            });
            MutateJsonList<Material, object>(MaterialFile, "materials", list =>
            {
                bool changed = false;
                foreach (var item in list.Where(x => string.IsNullOrWhiteSpace(x.Code)).Reverse())
                {
                    item.Code = NextCode(MaterialSequenceFile, "MAT", list.Select(x => x.Code), "WL");
                    changed = true;
                }
                return new JsonMutationResult<object>(null, changed);
            });
        }

        static void AddCustomer(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Customer>(ReadBody(ctx.Request)); ValidateCustomer(item);
            var saved = MutateJsonList<Customer, Customer>(CustomerFile, "customers", list =>
            {
                if (list.Any(x => string.Equals(x.Company, item.Company, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该客户公司已经存在", 409);
                item.Id = Guid.NewGuid().ToString("N"); item.Code = NextCode(CustomerSequenceFile, "CRM", list.Select(x => x.Code), "KH"); item.Status = "启用"; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<Customer>(item, true);
            });
            Audit(user, "新增客户", saved.Code + " " + saved.Company); WriteJson(ctx, saved, 201);
        }

        static void UpdateCustomer(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Customer>(ReadBody(ctx.Request)); ValidateCustomer(input);
            var saved = MutateJsonList<Customer, Customer>(CustomerFile, "customers", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("客户不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (list.Any(x => x.Id != id && string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)))
                    throw new BusinessException("该客户公司已经存在", 409);
                EnsureCustomerReferenceLockForEdit(item, input);
                item.Company = input.Company; item.Contact = input.Contact; item.Phone = input.Phone; item.Bank = input.Bank; item.Account = input.Account; item.BankNo = input.BankNo; item.Address = input.Address; item.Receivable = input.Receivable; item.Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<Customer>(item, true);
            });
            Audit(user, "修改客户", saved.Code + " " + saved.Company); WriteJson(ctx, saved);
        }

        static void DeleteCustomer(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<Customer>(CustomerFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("客户不存在", 404);
                auditDetail = item.Code + " " + item.Company;
            });
            EnforceDeleteImpact("customer", id, user, ctx, auditDetail);
            MutateJsonList<Customer, object>(CustomerFile, "customers", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("客户不存在", 404);
                EnsureCustomerReferenceLockForDelete(item);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除客户", auditDetail); WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteCustomers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            EnforceBatchDeleteImpact("customer", ids, user, ctx);
            var deleted = MutateJsonList<Customer, int>(CustomerFile, "customers", list =>
            {
                var removed = list.Where(x => ids.Contains(x.Id)).ToList();
                if (removed.Count == 0) throw new BusinessException("未找到可删除的客户", 404);
                var blocked = removed.Where(item => IsCustomerReferenced(item.Id, item.Code, item.Company)).ToList();
                if (blocked.Count > 0) throw new BusinessException(ReferenceLockMessage, 409);
                foreach (var item in removed) list.Remove(item);
                return new JsonMutationResult<int>(removed.Count, true);
            });
            Audit(user, "批量删除客户", "共" + deleted + "条");
            WriteJson(ctx, new { ok = true, deleted = deleted });
        }

        static void BatchAddCustomers(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchCustomerRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条客户资料" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<Customer, object>(CustomerFile, "customers", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var batchCompanies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pending = new List<Customer>();
                foreach (var input in items)
                {
                    rowNo++;
                    try
                    {
                        ValidateCustomer(input);
                        if (list.Any(x => string.Equals(x.Company, input.Company, StringComparison.OrdinalIgnoreCase)) || batchCompanies.Contains(input.Company))
                        {
                            skipped++; errors.Add("第" + rowNo + "行：客户已存在"); continue;
                        }
                        var item = new Customer
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Code = NextCode(CustomerSequenceFile, "CRM", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "KH"),
                            Company = input.Company, Contact = input.Contact, Phone = input.Phone, Bank = input.Bank,
                            Account = input.Account, BankNo = input.BankNo, Address = input.Address, Receivable = input.Receivable,
                            Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                            UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                        };
                        batchCompanies.Add(item.Company);
                        pending.Insert(0, item);
                        imported++;
                    }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加客户", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static readonly string[] MaterialStockTypes = { "原材料", "外购配件", "标准件", "成品设备", "维修备件", "低值易耗品", "其他" };
        static readonly string[] MaterialCostMethods = { "固定成本价", "最近采购价", "机型成本快照", "手动指定" };

        static List<Material> LoadMaterials()
        {
            lock (DataLock)
            {
                var list = ReadJsonListCore<Material>(MaterialFile);
                foreach (var item in list) NormalizeMaterialInventoryFields(item);
                return list;
            }
        }

        static string NormalizeMaterialStockType(string raw)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return "外购配件";
            foreach (var t in MaterialStockTypes)
                if (string.Equals(t, s, StringComparison.OrdinalIgnoreCase)) return t;
            return "其他";
        }

        static string NormalizeMaterialCostMethod(string raw)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return "固定成本价";
            foreach (var t in MaterialCostMethods)
                if (string.Equals(t, s, StringComparison.OrdinalIgnoreCase)) return t;
            return "固定成本价";
        }

        static bool ParseImportBool(string raw, bool defaultValue)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return defaultValue;
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "是" || s == "Y" || s == "y") return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "否" || s == "N" || s == "n") return false;
            return defaultValue;
        }

        static void NormalizeMaterialInventoryFields(Material item)
        {
            if (item == null) return;
            item.StockType = NormalizeMaterialStockType(item.StockType);
            item.IsInventoryItem = item.IsInventoryItem ?? true;
            item.DefaultWarehouse = string.IsNullOrWhiteSpace(item.DefaultWarehouse) ? "默认仓库" : item.DefaultWarehouse.Trim();
            item.CostMethod = NormalizeMaterialCostMethod(item.CostMethod);
            if (item.SafetyStock < 0) item.SafetyStock = 0;
            if (item.StockType == "成品设备") item.IsFinishedGood = true;
            if (item.StockType == "维修备件") item.IsServicePart = true;
            if (item.IsFinishedGood) item.StockType = "成品设备";
            if (item.IsServicePart && item.StockType != "成品设备") item.StockType = "维修备件";
        }

        static void ApplyMaterialInventoryFromImportRow(Dictionary<string, string> row, Material item)
        {
            if (item == null) return;
            string stockType = Cell(row, "库存类型", "StockType");
            if (!string.IsNullOrWhiteSpace(stockType)) item.StockType = stockType;
            string inv = Cell(row, "是否纳入库存", "IsInventoryItem");
            if (!string.IsNullOrWhiteSpace(inv)) item.IsInventoryItem = ParseImportBool(inv, true);
            string finished = Cell(row, "是否成品", "IsFinishedGood");
            if (!string.IsNullOrWhiteSpace(finished)) item.IsFinishedGood = ParseImportBool(finished, false);
            string service = Cell(row, "是否维修备件", "IsServicePart");
            if (!string.IsNullOrWhiteSpace(service)) item.IsServicePart = ParseImportBool(service, false);
            string safety = Cell(row, "安全库存", "SafetyStock");
            if (!string.IsNullOrWhiteSpace(safety)) item.SafetyStock = Money(safety);
            string wh = Cell(row, "默认仓库", "DefaultWarehouse");
            if (!string.IsNullOrWhiteSpace(wh)) item.DefaultWarehouse = wh;
            string costMethod = Cell(row, "成本方式", "CostMethod");
            if (!string.IsNullOrWhiteSpace(costMethod)) item.CostMethod = costMethod;
            NormalizeMaterialInventoryFields(item);
        }

        static string ComputeStockStatusLabel(decimal qty, decimal safetyStock)
        {
            if (qty <= 0) return "缺货";
            if (safetyStock > 0 && qty < safetyStock) return "偏低";
            return "正常";
        }

        static Material FindMaterialForStockItem(string itemType, string itemId, string itemCode)
        {
            if (string.Equals(itemType, "成品", StringComparison.OrdinalIgnoreCase)) return null;
            var materials = LoadMaterials();
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                var byId = materials.FirstOrDefault(x => x.Id == itemId);
                if (byId != null) return byId;
            }
            if (!string.IsNullOrWhiteSpace(itemCode))
                return materials.FirstOrDefault(x => string.Equals(x.Code, itemCode, StringComparison.OrdinalIgnoreCase));
            return null;
        }

        static void SaveMaterials(List<Material> items)
        {
            lock (DataLock) WriteJsonListCore(MaterialFile, "materials", items);
        }

        static void AddMaterial(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Material>(ReadBody(ctx.Request)); ValidateMaterial(item);
            NormalizeMaterialInventoryFields(item);
            var saved = MutateJsonList<Material, Material>(MaterialFile, "materials", list =>
            {
                item.Id = Guid.NewGuid().ToString("N"); item.Code = NextCode(MaterialSequenceFile, "MAT", list.Select(x => x.Code), "WL"); item.PriceType = NormalizePriceType(item.PriceType); item.Status = "启用"; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<Material>(item, true);
            });
            Audit(user, "新增物料", saved.Code + " " + saved.NameSpec); WriteJson(ctx, saved, 201);
        }

        static void UpdateMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Material>(ReadBody(ctx.Request)); ValidateMaterial(input);
            var saved = MutateJsonList<Material, Material>(MaterialFile, "materials", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("物料不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureMaterialReferenceLockForEdit(item, input);
                item.Supplier = input.Supplier; item.NameSpec = input.NameSpec; item.QuantityUnit = input.QuantityUnit; item.TaxPrice = input.TaxPrice; item.NoTaxPrice = input.NoTaxPrice; item.PriceType = NormalizePriceType(input.PriceType); item.Note = input.Note; item.Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status;
                item.StockType = input.StockType; item.IsInventoryItem = input.IsInventoryItem; item.IsFinishedGood = input.IsFinishedGood; item.IsServicePart = input.IsServicePart; item.SafetyStock = input.SafetyStock; item.DefaultWarehouse = input.DefaultWarehouse; item.CostMethod = input.CostMethod;
                NormalizeMaterialInventoryFields(item);
                item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<Material>(item, true);
            });
            Audit(user, "修改物料", saved.Code + " " + saved.NameSpec); WriteJson(ctx, saved);
        }

        static void DeleteMaterial(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<Material>(MaterialFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("物料不存在", 404);
                auditDetail = item.Code + " " + item.NameSpec;
            });
            EnforceDeleteImpact("material", id, user, ctx, auditDetail);
            MutateJsonList<Material, object>(MaterialFile, "materials", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("物料不存在", 404);
                EnsureMaterialReferenceLockForDelete(item);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除物料", auditDetail); WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteMaterials(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            EnforceBatchDeleteImpact("material", ids, user, ctx);
            var deleted = MutateJsonList<Material, int>(MaterialFile, "materials", list =>
            {
                var removed = list.Where(x => ids.Contains(x.Id)).ToList();
                if (removed.Count == 0) throw new BusinessException("未找到可删除的物料", 404);
                var blocked = removed.Where(item => IsMaterialReferenced(item.Id, item.Code)).ToList();
                if (blocked.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var item in blocked.Take(10))
                    {
                        var boms = GetBomsUsingMaterial(item.Id, item.Code);
                        var bomRefs = string.Join("、", boms.Take(3).Select(b => (b.Code ?? "") + " " + b.ModelName).Select(x => x.Trim()).Where(x => x.Length > 0));
                        parts.Add((item.Code ?? "") + " " + item.NameSpec + (bomRefs.Length > 0 ? "（BOM：" + bomRefs + "）" : ""));
                    }
                    string msg = blocked.Any(item => IsMaterialUsedByBom(item.Id, item.Code))
                        ? "以下物料已被业务引用，不能删除。 " + string.Join("；", parts)
                        : ReferenceLockMessage + " " + string.Join("；", parts);
                    if (blocked.Count > 10) msg += " 等共" + blocked.Count + "条";
                    throw new BusinessException(msg, 409);
                }
                foreach (var item in removed) list.Remove(item);
                return new JsonMutationResult<int>(removed.Count, true);
            });
            Audit(user, "批量删除物料", "共" + deleted + "条");
            WriteJson(ctx, new { ok = true, deleted = deleted });
        }

        static void BatchAddMaterials(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchMaterialRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条物料资料" }, 400); return; }
            var suppliers = LoadSuppliers();
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<Material, object>(MaterialFile, "materials", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var pending = new List<Material>();
                foreach (var input in items)
                {
                    rowNo++;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(input.NameSpec)) { skipped++; errors.Add("第" + rowNo + "行：物料名称/规格不能为空"); continue; }
                        if (string.IsNullOrWhiteSpace(input.Supplier)) { skipped++; errors.Add("第" + rowNo + "行：请选择供应商"); continue; }
                        input.Supplier = input.Supplier.Trim(); input.NameSpec = input.NameSpec.Trim();
                        input.QuantityUnit = NormalizeMaterialQuantityUnit(input.QuantityUnit);
                        input.Note = (input.Note ?? "").Trim();
                        if (!suppliers.Any(x => string.Equals(x.Company, input.Supplier, StringComparison.OrdinalIgnoreCase)))
                        {
                            skipped++; errors.Add("第" + rowNo + "行：供应商未建档"); continue;
                        }
                        if (!string.IsNullOrWhiteSpace(input.Code))
                        {
                            string code = input.Code.Trim();
                            if (list.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ||
                                pending.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                            {
                                skipped++; errors.Add("第" + rowNo + "行：物料编号已存在"); continue;
                            }
                        }
                        var item = new Material
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Code = !string.IsNullOrWhiteSpace(input.Code) ? input.Code.Trim() : NextCode(MaterialSequenceFile, "MAT", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "WL"),
                            Supplier = input.Supplier, NameSpec = input.NameSpec, QuantityUnit = input.QuantityUnit,
                            TaxPrice = input.TaxPrice, NoTaxPrice = input.NoTaxPrice, PriceType = NormalizePriceType(input.PriceType), Note = input.Note,
                            Status = string.IsNullOrEmpty(input.Status) ? "启用" : input.Status,
                            StockType = input.StockType, IsInventoryItem = input.IsInventoryItem, IsFinishedGood = input.IsFinishedGood, IsServicePart = input.IsServicePart,
                            SafetyStock = input.SafetyStock, DefaultWarehouse = input.DefaultWarehouse, CostMethod = input.CostMethod,
                            UpdatedAt = ProfileUpdatedAtNow(), UpdatedBy = user.DisplayName
                        };
                        NormalizeMaterialPriceFields(item);
                        NormalizeMaterialInventoryFields(item);
                        pending.Insert(0, item);
                        imported++;
                    }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加物料", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static List<FinanceTransaction> LoadFinance()
        {
            lock (DataLock) return ReadJsonListCore<FinanceTransaction>(FinanceFile);
        }

        static void SaveFinance(List<FinanceTransaction> items)
        {
            lock (DataLock) WriteJsonListCore(FinanceFile, "finance", items);
        }

        static OpeningBalances LoadOpeningBalances()
        {
            lock (DataLock)
            {
                string text = File.ReadAllText(OpeningFile, Encoding.UTF8);
                return Json.Deserialize<OpeningBalances>(text) ?? new OpeningBalances();
            }
        }

        static void SaveOpeningBalances(HttpListenerContext ctx, UserSession user)
        {
            var value = Json.Deserialize<OpeningBalances>(ReadBody(ctx.Request)) ?? new OpeningBalances();
            RunUnderDataLock(() =>
            {
                OpeningBalances current;
                if (File.Exists(OpeningFile))
                {
                    string text = File.ReadAllText(OpeningFile, Encoding.UTF8);
                    current = Json.Deserialize<OpeningBalances>(text) ?? new OpeningBalances();
                }
                else current = new OpeningBalances();
                EnsureEditVersionMatch(current.UpdatedAt, value.UpdatedAt);
                value.UpdatedAt = ProfileUpdatedAtNow();
                if (File.Exists(OpeningFile)) File.Copy(OpeningFile, Path.Combine(BackupDir, "opening_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json"), true);
                File.WriteAllText(OpeningFile, Json.Serialize(value), new UTF8Encoding(false));
                CleanBackups();
            });
            Audit(user, "修改期初余额", "公户=" + value.PublicAccount + ",公司私户=" + value.CompanyPrivate + ",个人私户=" + value.PersonalPrivate);
            WriteJson(ctx, value);
        }

        static void AddFinance(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<FinanceTransaction>(ReadBody(ctx.Request)); ValidateFinance(item);
            item.Id = Guid.NewGuid().ToString("N"); item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
            var saved = MutateJsonList<FinanceTransaction, FinanceTransaction>(FinanceFile, "finance", list =>
            {
                list.Add(item);
                return new JsonMutationResult<FinanceTransaction>(item, true);
            });
            Audit(user, "新增收支", saved.Date + " " + saved.AccountType + " " + saved.Purpose); WriteJson(ctx, saved, 201);
        }

        static void UpdateFinance(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<FinanceTransaction>(ReadBody(ctx.Request)); ValidateFinance(input);
            var saved = MutateJsonList<FinanceTransaction, FinanceTransaction>(FinanceFile, "finance", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("收支记录不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (IsFinanceReceiptLinked(item)) BizFail("该收支由应收收款自动联动生成，请先在应收款中修改或删除对应收款明细。", 409);
                item.Date = input.Date; item.AccountType = input.AccountType; item.Receipt = input.Receipt; item.Payment = input.Payment; item.PaymentMethod = input.PaymentMethod; item.Purpose = input.Purpose; item.Counterparty = input.Counterparty; item.Note = input.Note; item.UpdatedAt = ProfileUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<FinanceTransaction>(item, true);
            });
            Audit(user, "修改收支", saved.Date + " " + saved.AccountType + " " + saved.Purpose); WriteJson(ctx, saved);
        }

        static void DeleteFinance(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            MutateJsonList<FinanceTransaction, object>(FinanceFile, "finance", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("收支记录不存在", 404);
                if (IsFinanceReceiptLinked(item)) BizFail("该收支由应收收款自动联动生成，请先在应收款中修改或删除对应收款明细。", 409);
                auditDetail = item.Date + " " + item.AccountType + " " + item.Purpose;
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除收支", auditDetail); WriteJson(ctx, new { ok = true });
        }

        static void ExportFinanceTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("收支类型,日期,分类,金额,对方单位/客户/供应商,摘要/备注,经办人,付款方式/收款方式,账户类型");
            sb.AppendLine("收入,2026-01-01,整机销售,10000,示例客户,示例摘要,张三,公司公户（现金）,公户");
            WriteCsvDownload(ctx, "财务收支导入模板.csv", sb.ToString());
        }

        static bool TryParseFinanceDate(string value, out DateTime date)
        {
            value = (value ?? "").Trim();
            if (DateTime.TryParse(value, out date)) return true;
            double serial;
            if (double.TryParse(value, out serial) && serial > 0 && serial < 2958466)
            {
                try { date = DateTime.FromOADate(serial); return true; } catch { }
            }
            date = default(DateTime);
            return false;
        }

        static void ImportFinance(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<ImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择导入文件" }, 400); return; }
            List<Dictionary<string, string>> rows;
            if ((req.FileName ?? "").EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                rows = ReadImportRowsFromRequest(req);
            else if ((req.FileName ?? "").EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                rows = ReadCsvImportRowsFromRequest(new CsvImportRequest { FileName = req.FileName, Data = req.Data });
            else { WriteJson(ctx, new { error = "仅支持 .csv 或 .xlsx 格式文件" }, 400); return; }

            var errors = new List<string>();
            int imported = 0, rowNo = 1;
            MutateJsonList<FinanceTransaction, object>(FinanceFile, "finance", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    string type = Cell(row, "收支类型", "类型");
                    string dateText = Cell(row, "日期", "收支日期");
                    string category = Cell(row, "分类", "收付款用途", "用途");
                    string amountText = Cell(row, "金额", "收支金额");
                    if (type != "收入" && type != "支出") throw new Exception("收支类型必须是收入或支出");
                    DateTime date;
                    if (string.IsNullOrWhiteSpace(dateText)) throw new Exception("日期不能为空");
                    if (!TryParseFinanceDate(dateText, out date)) throw new Exception("日期格式不正确");
                    if (string.IsNullOrWhiteSpace(category)) throw new Exception("分类不能为空");
                    decimal amount;
                    if (string.IsNullOrWhiteSpace(amountText)) throw new Exception("金额不能为空");
                    if (!TryParseDecimalField(amountText.Replace("¥", "").Replace("￥", "").Replace(",", ""), out amount)) throw new Exception("金额必须为数字");
                    if (amount <= 0) throw new Exception("金额必须大于 0");
                    string account = Cell(row, "账户类型", "账户");
                    if (string.IsNullOrWhiteSpace(account)) account = "公户";
                    var item = new FinanceTransaction {
                        Id = Guid.NewGuid().ToString("N"), Date = date.ToString("yyyy-MM-dd"), AccountType = account,
                        Receipt = type == "收入" ? amount : 0, Payment = type == "支出" ? amount : 0,
                        Purpose = category, Counterparty = Cell(row, "对方单位/客户/供应商", "对方单位", "客户", "供应商", "对方账户主体"),
                        Note = Cell(row, "摘要/备注", "摘要", "备注"), PaymentMethod = Cell(row, "付款方式/收款方式", "付款方式", "收款方式", "收付款方式"),
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), UpdatedBy = Cell(row, "经办人", "操作人")
                    };
                    if (string.IsNullOrWhiteSpace(item.UpdatedBy)) item.UpdatedBy = user.DisplayName;
                    ValidateFinance(item);
                    list.Add(item); imported++;
                }
                catch (Exception ex) { errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user, "导入财务收支", "成功" + imported + "条，失败" + errors.Count + "条");
            WriteJson(ctx, new { imported = imported, failed = errors.Count, errors = errors.ToArray() });
        }

        static void ValidateFinance(FinanceTransaction item)
        {
            if (item == null) throw new Exception("收支记录不能为空");
            DateTime date;
            if (string.IsNullOrWhiteSpace(item.Date) || !DateTime.TryParse(item.Date, out date)) throw new Exception("请选择正确的日期");
            string[] accounts = { "公户", "公司私户", "个人私户" };
            if (!accounts.Contains(item.AccountType)) throw new Exception("请选择正确的账户类型");
            if (item.Receipt < 0 || item.Payment < 0) throw new Exception("收付款金额不能为负数");
            if ((item.Receipt > 0 && item.Payment > 0) || (item.Receipt == 0 && item.Payment == 0)) throw new Exception("每笔记录只能填写收款或付款其中一项");
            item.Date = date.ToString("yyyy-MM-dd");
            item.PaymentMethod = (item.PaymentMethod ?? "").Trim(); item.Purpose = (item.Purpose ?? "").Trim(); item.Counterparty = (item.Counterparty ?? "").Trim(); item.Note = (item.Note ?? "").Trim();
        }

        static void Validate(Supplier item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Company)) throw new Exception("供应商公司名不能为空");
            item.Company = item.Company.Trim();
            if (item.Company.Length > 100) throw new Exception("供应商公司名过长");
            item.Contact=(item.Contact??"").Trim(); item.Phone=(item.Phone??"").Trim(); item.Goods=(item.Goods??"").Trim(); item.Address=(item.Address??"").Trim(); item.Bank=(item.Bank??"").Trim(); item.Account=(item.Account??"").Trim(); item.BankNo=(item.BankNo??"").Trim();
        }

        static void ValidateCustomer(Customer item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Company)) throw new Exception("客户公司名不能为空");
            item.Company = item.Company.Trim();
            if (item.Company.Length > 100) throw new Exception("客户公司名过长");
            item.Contact=(item.Contact??"").Trim(); item.Phone=(item.Phone??"").Trim(); item.Bank=(item.Bank??"").Trim(); item.Account=(item.Account??"").Trim(); item.BankNo=(item.BankNo??"").Trim(); item.Address=(item.Address??"").Trim();
        }

        static void ValidateMaterial(Material item)
        {
            if(item==null||string.IsNullOrWhiteSpace(item.NameSpec))throw new Exception("物料名称/规格不能为空");
            if(string.IsNullOrWhiteSpace(item.Supplier))throw new Exception("请选择供应商");
            item.Supplier=item.Supplier.Trim();item.NameSpec=item.NameSpec.Trim();item.QuantityUnit=NormalizeMaterialQuantityUnit(item.QuantityUnit);item.Note=(item.Note??"").Trim();
            if(!LoadSuppliers().Any(x=>string.Equals(x.Company,item.Supplier,StringComparison.OrdinalIgnoreCase)))throw new Exception("所选供应商不在供应商管理中，请先建立供应商档案");
            NormalizeMaterialPriceFields(item);
            NormalizeMaterialInventoryFields(item);
        }

        static string NormalizeMaterialQuantityUnit(string raw)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return "1 件";
            var m = System.Text.RegularExpressions.Regex.Match(s, @"^(\d+(?:\.\d+)?)\s*(.*)$");
            if (!m.Success) return "1 " + s;
            decimal baseQty;
            if (!decimal.TryParse(m.Groups[1].Value, out baseQty) || baseQty <= 0) baseQty = 1;
            var unit = (m.Groups[2].Value ?? "").Trim();
            if (string.IsNullOrEmpty(unit)) unit = "件";
            return baseQty.ToString("0.##") + " " + unit;
        }

        static void NormalizeMaterialPriceFields(Material item)
        {
            if (item.TaxPrice > 0 && item.NoTaxPrice > 0)
                throw new BusinessException("含税价和不含税价只能填写一个", 422);
            if (item.TaxPrice > 0)
            {
                item.PriceType = "含税";
                item.NoTaxPrice = 0;
                return;
            }
            if (item.NoTaxPrice > 0)
            {
                item.PriceType = "不含税";
                item.TaxPrice = 0;
                return;
            }
            item.PriceType = NormalizePriceType(item.PriceType);
        }

        static List<Dictionary<string,string>> ReadImportRows(HttpListenerContext ctx)
        {
            return ReadImportRowsFromRequest(Json.Deserialize<ImportRequest>(ReadBody(ctx.Request)));
        }

        static List<Dictionary<string,string>> ReadImportRowsFromRequest(ImportRequest req)
        {
            if(req==null||string.IsNullOrWhiteSpace(req.Data))throw new Exception("请选择要导入的 Excel 文件");
            if(!string.IsNullOrEmpty(req.FileName)&&!req.FileName.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase))throw new Exception("仅支持 .xlsx 格式文件");
            string encoded=req.Data;int comma=encoded.IndexOf(',');if(comma>=0)encoded=encoded.Substring(comma+1);
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch{throw new Exception("Excel 文件内容无效");}
            if(bytes.Length>25*1024*1024)throw new Exception("Excel 文件不能超过 25MB");
            var table=new List<List<string>>();
            using(var ms=new MemoryStream(bytes))using(var zip=new ZipArchive(ms,ZipArchiveMode.Read))
            {
                var shared=new List<string>();var sharedEntry=zip.GetEntry("xl/sharedStrings.xml");
                if(sharedEntry!=null)using(var s=sharedEntry.Open()){var doc=XDocument.Load(s);XNamespace ns=doc.Root.Name.Namespace;foreach(var si in doc.Descendants(ns+"si"))shared.Add(string.Concat(si.Descendants(ns+"t").Select(x=>x.Value)));}
                var sheetEntry=zip.GetEntry("xl/worksheets/sheet1.xml");if(sheetEntry==null)throw new Exception("Excel 中没有可读取的工作表");
                using(var s=sheetEntry.Open())
                {
                    var doc=XDocument.Load(s);XNamespace ns=doc.Root.Name.Namespace;
                    foreach(var row in doc.Descendants(ns+"row"))
                    {
                        var values=new Dictionary<int,string>();int max=-1;
                        foreach(var c in row.Elements(ns+"c"))
                        {
                            string reference=(string)c.Attribute("r")??"A1";int col=0;foreach(char ch in reference){if(ch<'A'||ch>'Z')break;col=col*26+(ch-'A'+1);}col--;
                            string type=(string)c.Attribute("t")??"",value="";
                            if(type=="inlineStr")value=string.Concat(c.Descendants(ns+"t").Select(x=>x.Value));else{var v=c.Element(ns+"v");if(v!=null)value=v.Value;if(type=="s"){int index;if(int.TryParse(value,out index)&&index>=0&&index<shared.Count)value=shared[index];}}
                            values[col]=value;max=Math.Max(max,col);
                        }
                        if(max>=0){var cells=new List<string>();for(int i=0;i<=max;i++){string value;cells.Add(values.TryGetValue(i,out value)?value.Trim():"");}table.Add(cells);}
                    }
                }
            }
            if(table.Count<1)throw new Exception("Excel 表格没有表头");
            var headers=table[0];var result=new List<Dictionary<string,string>>();
            foreach(var cells in table.Skip(1)){var row=new Dictionary<string,string>();for(int i=0;i<headers.Count;i++)if(!string.IsNullOrWhiteSpace(headers[i]))row[headers[i]]=i<cells.Count?cells[i]:"";result.Add(row);}
            return result;
        }

        static string Cell(Dictionary<string,string> row, params string[] names){foreach(var name in names){string value;if(row.TryGetValue(name,out value))return(value??"").Trim();}return"";}
        static decimal Money(string value){decimal n;value=(value??"").Replace(",","").Replace("￥","").Replace("¥","").Trim();return decimal.TryParse(value,out n)?n:0;}
        static bool Placeholder(string value){return string.IsNullOrWhiteSpace(value)||value.Contains("必填选项")||value.Contains("要求自动生成");}

        static void ImportSuppliers(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            MutateJsonList<Supplier, object>(DataFile, "auto", list =>
            {
                foreach(var row in rows){rowNo++;string company=Cell(row,"供应商名称","供应商公司名");if(Placeholder(company)){skipped++;continue;}if(list.Any(x=>string.Equals(x.Company,company,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：供应商已存在");continue;}var item=new Supplier{Id=Guid.NewGuid().ToString("N"),Code=NextCode(SupplierSequenceFile, "SRM", list.Select(x=>x.Code), "GY"),Company=company,Contact=Cell(row,"联系人"),Phone=Cell(row,"联系电话"),Goods=Cell(row,"供应商品"),Address=Cell(row,"单位地址"),Bank=Cell(row,"开户行"),Account=Cell(row,"银行账号"),BankNo=Cell(row,"开户行行号"),Payable=Money(Cell(row,"当前应付款")),Status=Cell(row,"状态"),UpdatedAt=ProfileUpdatedAtNow(),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
                return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user,"导入供应商","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static void ImportCustomers(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            MutateJsonList<Customer, object>(CustomerFile, "customers", list =>
            {
                foreach(var row in rows){rowNo++;string company=Cell(row,"客户名称","公司名");if(Placeholder(company)){skipped++;continue;}if(list.Any(x=>string.Equals(x.Company,company,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：客户已存在");continue;}var item=new Customer{Id=Guid.NewGuid().ToString("N"),Code=NextCode(CustomerSequenceFile, "CRM", list.Select(x=>x.Code), "KH"),Company=company,Contact=Cell(row,"联系人"),Phone=Cell(row,"联系电话"),Bank=Cell(row,"开户行"),Account=Cell(row,"银行账号"),BankNo=Cell(row,"开户行行号"),Address=Cell(row,"地址"),Receivable=Money(Cell(row,"实时当前应收款")),Status=Cell(row,"状态"),UpdatedAt=ProfileUpdatedAtNow(),UpdatedBy=user.DisplayName};if(string.IsNullOrEmpty(item.Status))item.Status="启用";list.Insert(0,item);imported++;}
                return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user,"导入客户","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static decimal MaterialDisplayUnitPrice(Material x)
        {
            if (x == null) return 0;
            return NormalizePriceType(x.PriceType) == "含税" ? x.TaxPrice : x.NoTaxPrice;
        }

        static (decimal BaseQty, string Unit) ParseMaterialQtyUnitParts(string raw)
        {
            var normalized = NormalizeMaterialQuantityUnit(raw);
            var m = System.Text.RegularExpressions.Regex.Match(normalized, @"^(\d+(?:\.\d+)?)\s*(.*)$");
            if (!m.Success) return (1, "件");
            decimal baseQty;
            if (!decimal.TryParse(m.Groups[1].Value, out baseQty) || baseQty <= 0) baseQty = 1;
            var unit = (m.Groups[2].Value ?? "").Trim();
            if (string.IsNullOrEmpty(unit)) unit = "件";
            return (baseQty, unit);
        }

        static Material BuildMaterialFromImportRow(Dictionary<string, string> row)
        {
            var item = new Material();
            item.Supplier = Cell(row, "供应商");
            item.NameSpec = Cell(row, "物料名称/规格");
            string baseQty = Cell(row, "基准数量");
            string unit = Cell(row, "单位");
            string legacyQty = Cell(row, "数量/单位");
            if (!string.IsNullOrWhiteSpace(baseQty) || !string.IsNullOrWhiteSpace(unit))
                item.QuantityUnit = NormalizeMaterialQuantityUnit((baseQty ?? "1").Trim() + " " + (unit ?? "件").Trim());
            else
                item.QuantityUnit = NormalizeMaterialQuantityUnit(legacyQty);
            string unitPriceText = Cell(row, "单价");
            string priceType = Cell(row, "价格类型");
            if (!string.IsNullOrWhiteSpace(unitPriceText))
            {
                var price = Money(unitPriceText);
                priceType = NormalizePriceType(string.IsNullOrWhiteSpace(priceType) ? "不含税" : priceType);
                item.PriceType = priceType;
                item.TaxPrice = priceType == "含税" ? price : 0;
                item.NoTaxPrice = priceType == "不含税" ? price : 0;
            }
            else
            {
                item.TaxPrice = Money(Cell(row, "含税价"));
                item.NoTaxPrice = Money(Cell(row, "不含税价"));
                item.PriceType = NormalizePriceType(priceType);
            }
            item.Note = Cell(row, "备注");
            item.Status = Cell(row, "状态");
            if (string.IsNullOrEmpty(item.Status)) item.Status = "启用";
            ApplyMaterialInventoryFromImportRow(row, item);
            NormalizeMaterialPriceFields(item);
            return item;
        }

        static void ImportMaterials(HttpListenerContext ctx,UserSession user)
        {
            var rows=ReadImportRows(ctx);var suppliers=LoadSuppliers();int imported=0,skipped=0;var errors=new List<string>();int rowNo=1;
            MutateJsonList<Material, object>(MaterialFile, "materials", list =>
            {
                var pending = new List<Material>();
                foreach(var row in rows){
                    rowNo++;
                    try {
                        string supplier=Cell(row,"供应商"),name=Cell(row,"物料名称/规格");
                        if(Placeholder(name)){skipped++;continue;}
                        if(!suppliers.Any(x=>string.Equals(x.Company,supplier,StringComparison.OrdinalIgnoreCase))){skipped++;errors.Add("第"+rowNo+"行：供应商未建档");continue;}
                        string importCode = (Cell(row, "物料编号") ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(importCode))
                        {
                            if (list.Any(x => string.Equals(x.Code, importCode, StringComparison.OrdinalIgnoreCase)) ||
                                pending.Any(x => string.Equals(x.Code, importCode, StringComparison.OrdinalIgnoreCase)))
                            {
                                skipped++; errors.Add("第" + rowNo + "行：物料编号已存在"); continue;
                            }
                        }
                        var item=BuildMaterialFromImportRow(row);
                        item.Supplier=supplier;item.NameSpec=name;
                        ValidateMaterial(item);
                        item.Id=Guid.NewGuid().ToString("N");
                        item.Code = !string.IsNullOrWhiteSpace(importCode) ? importCode : NextCode(MaterialSequenceFile,"MAT",list.Select(x=>x.Code).Concat(pending.Select(x=>x.Code)), "WL");
                        item.UpdatedAt=ProfileUpdatedAtNow();item.UpdatedBy=user.DisplayName;
                        pending.Insert(0, item);
                        list.Insert(0,item);imported++;
                    } catch (Exception ex) { skipped++; errors.Add("第"+rowNo+"行："+ToUserMessage(ex)); }
                }
                return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user,"导入物料","成功"+imported+"条，跳过"+skipped+"条");WriteJson(ctx,new{imported=imported,skipped=skipped,errors=errors.Take(8).ToArray()});
        }

        static string ExportPriceTypeLabel(string priceType) { return NormalizePriceType(priceType) == "含税" ? "含税价" : "不含税价"; }

        static bool TryNormalizeImportPriceType(string input, out string normalized)
        {
            var s = (input ?? "").Trim();
            if (s == "含税价" || s == "含税") { normalized = "含税"; return true; }
            if (s == "不含税价" || s == "不含税") { normalized = "不含税"; return true; }
            normalized = "";
            return false;
        }

        static bool TryParseDecimalField(string value, out decimal result)
        {
            value = (value ?? "").Replace(",", "").Replace("￥", "").Replace("¥", "").Trim();
            return decimal.TryParse(value, out result);
        }

        static string BomConflictKey(string code, string version) { return (code ?? "").Trim() + "|" + (version ?? "").Trim(); }

        static string ModelCostConflictKey(string modelCode, string bomCode, string bomVersion)
        {
            return (modelCode ?? "").Trim() + "|" + (bomCode ?? "").Trim() + "|" + (bomVersion ?? "").Trim();
        }

        static void BumpSequenceIfNeeded(string sequenceFile, string prefix, string code, params string[] legacyPrefixes)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            var prefixes = new List<string> { prefix };
            if (legacyPrefixes != null) prefixes.AddRange(legacyPrefixes.Where(x => !string.IsNullOrEmpty(x)));
            int value = ParseCodeSequence(code, prefixes.ToArray());
            if (value <= 0) return;
            lock (DataLock)
            {
                int sequence;
                if (!int.TryParse(File.ReadAllText(sequenceFile, Encoding.UTF8), out sequence)) sequence = 0;
                if (value > sequence) File.WriteAllText(sequenceFile, value.ToString(), new UTF8Encoding(false));
            }
        }

        static string DecodeCsvImportData(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) throw new Exception("请选择要导入的 CSV 文件");
            string encoded = data.Trim();
            if (encoded.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = encoded.IndexOf(',');
                if (comma >= 0) encoded = encoded.Substring(comma + 1);
                try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
                catch { throw new Exception("CSV 文件内容无效"); }
            }
            return encoded;
        }

        static List<string> ParseCsvLine(string line)
        {
            var cells = new List<string>();
            if (line == null) return cells;
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(ch);
                }
                else
                {
                    if (ch == '"') inQuotes = true;
                    else if (ch == ',') { cells.Add(current.ToString()); current.Clear(); }
                    else current.Append(ch);
                }
            }
            cells.Add(current.ToString());
            return cells;
        }

        static List<Dictionary<string, string>> ParseCsvText(string csvText)
        {
            var table = new List<List<string>>();
            using (var reader = new StringReader(csvText ?? ""))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    table.Add(ParseCsvLine(line));
                }
            }
            if (table.Count < 1) throw new Exception("CSV 表格没有表头");
            var headers = table[0];
            var result = new List<Dictionary<string, string>>();
            for (int r = 1; r < table.Count; r++)
            {
                var cells = table[r];
                var row = new Dictionary<string, string>();
                for (int i = 0; i < headers.Count; i++)
                {
                    string header = (headers[i] ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(header)) continue;
                    row[header] = i < cells.Count ? (cells[i] ?? "").Trim() : "";
                }
                if (row.Values.Any(v => !string.IsNullOrWhiteSpace(v))) result.Add(row);
            }
            return result;
        }

        static List<Dictionary<string, string>> ReadCsvImportRowsFromRequest(CsvImportRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) throw new Exception("请选择要导入的 CSV 文件");
            if (!string.IsNullOrEmpty(req.FileName) && !req.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                throw new Exception("仅支持 .csv 格式文件");
            string text = DecodeCsvImportData(req.Data);
            if (text.Length > 25 * 1024 * 1024) throw new Exception("CSV 文件不能超过 25MB");
            return ParseCsvText(text);
        }

        static List<Dictionary<string, string>> ReadCsvImportRows(HttpListenerContext ctx)
        {
            return ReadCsvImportRowsFromRequest(Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request)));
        }

        static void WriteCsvDownload(HttpListenerContext ctx, string filename, string csvContent)
        {
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csvContent)).ToArray();
            ctx.Response.ContentType = "text/csv; charset=utf-8";
            ctx.Response.AddHeader("Content-Disposition", BuildExportContentDisposition(filename));
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        static string BuildExportFileName(string moduleName, string ext = ".csv")
        {
            if (string.IsNullOrEmpty(ext)) ext = ".csv";
            if (!ext.StartsWith(".")) ext = "." + ext;
            return moduleName + DateTime.Now.ToString("yyyyMMdd") + ext;
        }

        static string BuildExportContentDisposition(string fileName)
        {
            var ext = Path.GetExtension(fileName ?? ".csv");
            if (string.IsNullOrEmpty(ext)) ext = ".csv";
            return "attachment; filename=export" + DateTime.Now.ToString("yyyyMMdd") + ext;
        }

        static readonly string[] BomCsvHeaders = new[] {
            "BOM编号","BOM版本","产品名称","机型编号","机型名称","总材料成本","BOM备注","BOM创建时间","BOM更新时间",
            "明细序号","物料编号","物料名称","规格","单位","用量","原始单价","价格类型","税率","不含税单价","金额","价格来源时间","明细备注"
        };

        static string BuildBomCsvRow(BomItem bom, BomDetail line, int lineNo)
        {
            var fields = new List<string>
            {
                bom.Code, bom.Version, bom.ProductName, bom.ModelCode, bom.ModelName,
                bom.TotalMaterialCost.ToString("0.00"), bom.Note, bom.CreatedAt, bom.UpdatedAt
            };
            if (line != null)
            {
                fields.Add(lineNo.ToString());
                fields.Add(line.MaterialCode);
                fields.Add(line.MaterialName);
                fields.Add(line.Spec);
                fields.Add(line.Unit);
                fields.Add(line.Quantity.ToString("0.####"));
                fields.Add(line.OriginalPrice.ToString("0.####"));
                fields.Add(ExportPriceTypeLabel(line.PriceType));
                fields.Add(line.TaxRate.ToString("0.##"));
                fields.Add(line.NoTaxPrice.ToString("0.####"));
                fields.Add(line.Amount.ToString("0.00"));
                fields.Add(line.PriceSourceTime);
                fields.Add(line.Note);
            }
            else
            {
                fields.Add("");
                fields.AddRange(new[] { "", "", "", "", "", "", "", "", "", "", "", "" });
            }
            return string.Join(",", fields.Select(Csv));
        }

        static void ExportBomCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", BomCsvHeaders.Select(Csv)));
            foreach (var bom in LoadBom())
            {
                var items = bom.Items ?? new List<BomDetail>();
                if (items.Count == 0) sb.AppendLine(BuildBomCsvRow(bom, null, 0));
                else for (int i = 0; i < items.Count; i++) sb.AppendLine(BuildBomCsvRow(bom, items[i], i + 1));
            }
            WriteCsvDownload(ctx, BuildExportFileName("BOM表"), sb.ToString());
        }

        static void ExportBomTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", BomCsvHeaders.Select(Csv)));
            sb.AppendLine(string.Join(",", new[] {
                "BOM-DEMO","V1","示例产品","MODEL-DEMO","示例机型","","示例备注","","",
                "1","WL-DEMO","示例物料","规格A","个","1","10","不含税价","10","10","10","","明细备注"
            }.Select(Csv)));
            WriteCsvDownload(ctx, "BOM导入模板.csv", sb.ToString());
        }

        static BomItem BuildBomFromImportGroup(List<Dictionary<string, string>> groupRows, decimal defaultTaxRate, List<Material> materials, List<string> warnings, List<string> errors, ref int failedRows, int firstRowNo)
        {
            var first = groupRows[0];
            string code = Cell(first, "BOM编号");
            string version = Cell(first, "BOM版本");
            if (string.IsNullOrWhiteSpace(code)) { errors.Add("第" + firstRowNo + "行：BOM编号不能为空"); failedRows++; return null; }
            if (string.IsNullOrWhiteSpace(version)) { errors.Add("第" + firstRowNo + "行：BOM版本不能为空"); failedRows++; return null; }
            var item = new BomItem
            {
                Code = code.Trim(),
                Version = version.Trim(),
                ProductName = Cell(first, "产品名称"),
                ModelCode = Cell(first, "机型编号"),
                ModelName = Cell(first, "机型名称"),
                Note = Cell(first, "BOM备注"),
                Status = "启用",
                Items = new List<BomDetail>()
            };
            if (string.IsNullOrWhiteSpace(item.ModelCode)) item.ModelCode = "-";
            if (string.IsNullOrWhiteSpace(item.ModelName)) item.ModelName = "-";
            if (string.IsNullOrWhiteSpace(item.ProductName)) item.ProductName = "-";
            int rowNo = firstRowNo;
            foreach (var row in groupRows)
            {
                string materialCode = Cell(row, "物料编号");
                if (string.IsNullOrWhiteSpace(materialCode))
                {
                    if (row != first)
                    {
                        string mc = Cell(row, "机型编号"), mn = Cell(row, "机型名称"), pn = Cell(row, "产品名称");
                        if (!string.IsNullOrWhiteSpace(mc)) item.ModelCode = mc;
                        if (!string.IsNullOrWhiteSpace(mn)) item.ModelName = mn;
                        if (!string.IsNullOrWhiteSpace(pn)) item.ProductName = pn;
                        if (!string.IsNullOrWhiteSpace(Cell(row, "BOM备注"))) item.Note = Cell(row, "BOM备注");
                    }
                    rowNo++;
                    continue;
                }
                string qtyText = Cell(row, "用量");
                decimal quantity;
                if (!TryParseDecimalField(qtyText, out quantity) || quantity <= 0)
                {
                    errors.Add("第" + rowNo + "行：用量必须是大于 0 的数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                string origText = Cell(row, "原始单价");
                string noTaxText = Cell(row, "不含税单价");
                string amountText = Cell(row, "金额");
                string taxText = Cell(row, "税率");
                decimal originalPrice, noTaxPrice, amount, taxRate;
                if (!string.IsNullOrWhiteSpace(origText) && !TryParseDecimalField(origText, out originalPrice))
                {
                    errors.Add("第" + rowNo + "行：原始单价必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(noTaxText) && !TryParseDecimalField(noTaxText, out noTaxPrice))
                {
                    errors.Add("第" + rowNo + "行：不含税单价必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(amountText) && !TryParseDecimalField(amountText, out amount))
                {
                    errors.Add("第" + rowNo + "行：金额必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(taxText) && !TryParseDecimalField(taxText, out taxRate))
                {
                    errors.Add("第" + rowNo + "行：税率必须是数字");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                string priceTypeRaw = Cell(row, "价格类型");
                string priceType;
                if (string.IsNullOrWhiteSpace(priceTypeRaw)) priceType = "不含税";
                else if (!TryNormalizeImportPriceType(priceTypeRaw, out priceType))
                {
                    errors.Add("第" + rowNo + "行：价格类型只能是含税价或不含税价");
                    failedRows++;
                    rowNo++;
                    continue;
                }
                taxRate = string.IsNullOrWhiteSpace(taxText) ? defaultTaxRate : Money(taxText);
                if (taxRate < 0) taxRate = 0;
                originalPrice = string.IsNullOrWhiteSpace(origText) ? 0 : Money(origText);
                noTaxPrice = string.IsNullOrWhiteSpace(noTaxText) ? 0 : Money(noTaxText);
                if (string.IsNullOrWhiteSpace(noTaxText) && !string.IsNullOrWhiteSpace(origText) && priceType == "含税")
                    noTaxPrice = CalcNoTaxUnitPrice(originalPrice, taxRate);
                else if (string.IsNullOrWhiteSpace(origText) && !string.IsNullOrWhiteSpace(noTaxText))
                    originalPrice = noTaxPrice;
                amount = string.IsNullOrWhiteSpace(amountText) ? CalcLineAmount(quantity, originalPrice) : Money(amountText);
                var line = new BomDetail
                {
                    MaterialCode = materialCode.Trim(),
                    MaterialName = Cell(row, "物料名称"),
                    Spec = string.IsNullOrWhiteSpace(Cell(row, "规格")) ? "-" : Cell(row, "规格"),
                    Unit = string.IsNullOrWhiteSpace(Cell(row, "单位")) ? "-" : Cell(row, "单位"),
                    Quantity = quantity,
                    OriginalPrice = originalPrice,
                    PriceType = priceType,
                    TaxRate = taxRate,
                    NoTaxPrice = noTaxPrice,
                    Amount = amount,
                    PriceSourceTime = Cell(row, "价格来源时间"),
                    Note = Cell(row, "明细备注")
                };
                if (string.IsNullOrWhiteSpace(line.PriceSourceTime))
                    line.PriceSourceTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var mat = materials.FirstOrDefault(m => string.Equals(m.Code, line.MaterialCode, StringComparison.OrdinalIgnoreCase));
                if (mat != null)
                {
                    line.MaterialId = mat.Id;
                    if (string.IsNullOrWhiteSpace(line.MaterialName)) line.MaterialName = mat.NameSpec;
                    if (line.Unit == "-") line.Unit = string.IsNullOrWhiteSpace(mat.QuantityUnit) ? "-" : mat.QuantityUnit;
                }
                else
                {
                    line.MaterialId = "";
                    warnings.Add("第" + rowNo + "行：物料编号 " + line.MaterialCode + " 不存在，仅作为 BOM 快照导入");
                }
                item.Items.Add(line);
                rowNo++;
            }
            return item;
        }

        static void ImportBomCsv(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择要导入的 CSV 文件" }, 400); return; }
            var rows = ReadCsvImportRowsFromRequest(req);
            var previewList = LoadBom();
            var materials = LoadMaterials();
            var taxRate = LoadSystemSettings().TaxRate;
            var groups = new Dictionary<string, List<Dictionary<string, string>>>();
            var groupFirstRow = new Dictionary<string, int>();
            int rowNo = 1;
            var errors = new List<string>();
            var warnings = new List<string>();
            int failedRows = 0;
            foreach (var row in rows)
            {
                rowNo++;
                string code = Cell(row, "BOM编号"), version = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(Cell(row, "物料编号")))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号和BOM版本不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(version))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号和BOM版本不能为空");
                    continue;
                }
                string key = BomConflictKey(code, version);
                if (!groups.ContainsKey(key)) { groups[key] = new List<Dictionary<string, string>>(); groupFirstRow[key] = rowNo; }
                groups[key].Add(row);
            }
            var conflicts = groups.Keys.Where(k => previewList.Any(x => string.Equals(x.Code, k.Split('|')[0], StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version ?? "", k.Split('|')[1], StringComparison.OrdinalIgnoreCase))).ToList();
            var actions = req.ConflictActions ?? new Dictionary<string, string>();
            if (conflicts.Count > 0)
            {
                var unresolved = conflicts.Where(k => !actions.ContainsKey(k) || string.IsNullOrWhiteSpace(actions[k])).ToList();
                if (unresolved.Count > 0)
                {
                    WriteJson(ctx, new TableImportResult { NeedsConflictDecision = true, Conflicts = conflicts.ToArray() });
                    return;
                }
            }
            int added = 0, updated = 0, skipped = 0;
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            MutateJsonList<BomItem, object>(BomFile, "bom", list =>
            {
            foreach (var kv in groups)
            {
                string key = kv.Key;
                var existing = list.FirstOrDefault(x => string.Equals(x.Code, key.Split('|')[0], StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version ?? "", key.Split('|')[1], StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    string action = (actions.ContainsKey(key) ? actions[key] : "").Trim().ToLowerInvariant();
                    if (action == "skip" || action == "跳过") { skipped++; continue; }
                    if (action != "overwrite" && action != "覆盖") { skipped++; continue; }
                }
                var item = BuildBomFromImportGroup(kv.Value, taxRate, materials, warnings, errors, ref failedRows, groupFirstRow[key]);
                if (item == null) continue;
                RecalcBomLines(item, taxRate);
                if (existing != null)
                {
                    item.Id = existing.Id;
                    item.Code = existing.Code;
                    item.CreatedAt = existing.CreatedAt;
                    item.UpdatedAt = now;
                    list[list.IndexOf(existing)] = item;
                    updated++;
                }
                else
                {
                    item.Id = Guid.NewGuid().ToString("N");
                    item.CreatedAt = string.IsNullOrWhiteSpace(Cell(kv.Value[0], "BOM创建时间")) ? now : Cell(kv.Value[0], "BOM创建时间");
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(kv.Value[0], "BOM更新时间")) ? now : Cell(kv.Value[0], "BOM更新时间");
                    BumpSequenceIfNeeded(BomSequenceFile, "BOM", item.Code);
                    list.Insert(0, item);
                    added++;
                }
            }
            return new JsonMutationResult<object>(null, added > 0 || updated > 0);
            });
            Audit(user, "导入BOM", "新增" + added + "，更新" + updated + "，跳过" + skipped + "，失败" + failedRows + "行");
            WriteJson(ctx, new TableImportResult { Added = added, Updated = updated, Skipped = skipped, FailedRows = failedRows, Errors = errors.ToArray(), Warnings = warnings.ToArray() });
        }

        static readonly string[] ModelCostCsvHeaders = new[] {
            "机型编号","机型名称","产品名称","BOM编号","BOM版本","材料成本","总成本","备注","状态","创建时间","更新时间"
        };

        static readonly string[] ModelCostIgnoredHeaders = new[] { "人工成本", "制造费用", "其他费用", "运费" };

        static void ExportModelCostsCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", ModelCostCsvHeaders.Select(Csv)));
            foreach (var x in LoadModelCosts())
            {
                sb.AppendLine(string.Join(",", new[] {
                    x.ModelCode, x.ModelName, x.ProductName, x.BomCode, x.BomVersion,
                    x.MaterialCost.ToString("0.00"), x.TotalCost.ToString("0.00"),
                    x.Note, string.IsNullOrWhiteSpace(x.Status) ? "启用" : x.Status, x.CreatedAt, x.UpdatedAt
                }.Select(Csv)));
            }
            WriteCsvDownload(ctx, BuildExportFileName("机型成本"), sb.ToString());
        }

        static void ExportModelCostTemplateCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", ModelCostCsvHeaders.Select(Csv)));
            sb.AppendLine(string.Join(",", new[] {
                "MODEL-DEMO","示例机型","示例产品","BOM-DEMO","V1","100","100","示例备注","启用","",""
            }.Select(Csv)));
            WriteCsvDownload(ctx, "机型成本导入模板.csv", sb.ToString());
        }

        static void ImportModelCostsCsv(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<CsvImportRequest>(ReadBody(ctx.Request));
            if (req == null || string.IsNullOrWhiteSpace(req.Data)) { WriteJson(ctx, new { error = "请选择要导入的 CSV 文件" }, 400); return; }
            var rows = ReadCsvImportRowsFromRequest(req);
            var previewList = LoadModelCosts();
            var bomList = LoadBom();
            var errors = new List<string>();
            var warnings = new List<string>();
            int failedRows = 0, added = 0, updated = 0, skipped = 0;
            if (rows.Count > 0)
            {
                var allHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows) foreach (var k in row.Keys) allHeaders.Add(k);
                foreach (var h in ModelCostIgnoredHeaders)
                    if (allHeaders.Contains(h)) warnings.Add("导入文件包含已忽略字段：" + h);
            }
            var conflictKeys = new HashSet<string>();
            int previewRow = 1;
            foreach (var row in rows)
            {
                previewRow++;
                string modelCode = Cell(row, "机型编号"), bomCode = Cell(row, "BOM编号"), bomVersion = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(modelCode) || string.IsNullOrWhiteSpace(bomCode) || string.IsNullOrWhiteSpace(bomVersion)) continue;
                string key = ModelCostConflictKey(modelCode, bomCode, bomVersion);
                if (previewList.Any(x => string.Equals(x.ModelCode, modelCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomCode, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomVersion ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase)))
                    conflictKeys.Add(key);
            }
            var actions = req.ConflictActions ?? new Dictionary<string, string>();
            if (conflictKeys.Count > 0)
            {
                var unresolved = conflictKeys.Where(k => !actions.ContainsKey(k) || string.IsNullOrWhiteSpace(actions[k])).ToList();
                if (unresolved.Count > 0)
                {
                    WriteJson(ctx, new TableImportResult { NeedsConflictDecision = true, Conflicts = conflictKeys.ToArray() });
                    return;
                }
            }
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            int rowNo = 1;
            MutateJsonList<ModelCost, object>(ModelCostFile, "model_costs", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                string modelCode = Cell(row, "机型编号");
                string modelName = Cell(row, "机型名称");
                string bomCode = Cell(row, "BOM编号");
                string bomVersion = Cell(row, "BOM版本");
                if (string.IsNullOrWhiteSpace(modelCode))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：机型编号不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：机型名称不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(bomCode))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM编号不能为空");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(bomVersion))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM版本不能为空");
                    continue;
                }
                string matText = Cell(row, "材料成本");
                decimal materialCost;
                if (string.IsNullOrWhiteSpace(matText) || !TryParseDecimalField(matText, out materialCost))
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：材料成本必须是数字");
                    continue;
                }
                string totalText = Cell(row, "总成本");
                decimal totalCost = materialCost;
                if (!string.IsNullOrWhiteSpace(totalText))
                {
                    decimal parsedTotal;
                    if (!TryParseDecimalField(totalText, out parsedTotal))
                    {
                        failedRows++;
                        errors.Add("第" + rowNo + "行：总成本必须是数字");
                        continue;
                    }
                    if (parsedTotal != materialCost)
                        warnings.Add("第" + rowNo + "行：总成本已自动修正为材料成本");
                    totalCost = materialCost;
                }
                var bom = bomList.FirstOrDefault(b => string.Equals(b.Code, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(b.Version ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase));
                if (bom == null)
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：BOM " + bomCode + " " + bomVersion + " 不存在");
                    continue;
                }
                string key = ModelCostConflictKey(modelCode, bomCode, bomVersion);
                var existing = list.FirstOrDefault(x => string.Equals(x.ModelCode, modelCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomCode, bomCode.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(x.BomVersion ?? "", bomVersion.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    string action = (actions.ContainsKey(key) ? actions[key] : "").Trim().ToLowerInvariant();
                    if (action == "skip" || action == "跳过") { skipped++; continue; }
                    if (action != "overwrite" && action != "覆盖") { skipped++; continue; }
                }
                string status = Cell(row, "状态");
                if (string.IsNullOrWhiteSpace(status)) status = "启用";
                else status = status.Trim();
                if (status != "启用" && status != "停用")
                {
                    failedRows++;
                    errors.Add("第" + rowNo + "行：状态只能是启用或停用");
                    continue;
                }
                var item = new ModelCost
                {
                    ModelCode = modelCode.Trim(),
                    ModelName = modelName.Trim(),
                    ProductName = string.IsNullOrWhiteSpace(Cell(row, "产品名称")) ? bom.ProductName : Cell(row, "产品名称"),
                    BomId = bom.Id,
                    BomCode = bom.Code,
                    BomVersion = bom.Version,
                    MaterialCost = Math.Round(materialCost, 2),
                    TotalCost = Math.Round(totalCost, 2),
                    Note = Cell(row, "备注"),
                    Status = status
                };
                if (existing != null)
                {
                    item.Id = existing.Id;
                    item.CreatedAt = existing.CreatedAt;
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(row, "更新时间")) ? now : Cell(row, "更新时间");
                    list[list.IndexOf(existing)] = item;
                    updated++;
                }
                else
                {
                    item.Id = Guid.NewGuid().ToString("N");
                    item.CreatedAt = string.IsNullOrWhiteSpace(Cell(row, "创建时间")) ? now : Cell(row, "创建时间");
                    item.UpdatedAt = string.IsNullOrWhiteSpace(Cell(row, "更新时间")) ? now : Cell(row, "更新时间");
                    list.Insert(0, item);
                    added++;
                }
            }
            return new JsonMutationResult<object>(null, added > 0 || updated > 0);
            });
            Audit(user, "导入机型成本", "新增" + added + "，更新" + updated + "，跳过" + skipped + "，失败" + failedRows + "行");
            WriteJson(ctx, new TableImportResult { Added = added, Updated = updated, Skipped = skipped, FailedRows = failedRows, Errors = errors.ToArray(), Warnings = warnings.ToArray() });
        }

        static void ExportCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("供应商编号,供应商名称,联系人,联系电话,供应商品,单位地址,开户行,银行账号,开户行行号,当前应付款,状态,最后更新,操作人");
            foreach (var x in LoadSuppliers()) sb.AppendLine(string.Join(",", new[] { x.Code,x.Company,x.Contact,x.Phone,x.Goods,x.Address,x.Bank,x.Account,x.BankNo,x.Payable.ToString("0.00"),x.Status,x.UpdatedAt,x.UpdatedBy }.Select(Csv)));
            WriteCsvDownload(ctx, BuildExportFileName("供应商管理"), sb.ToString());
        }

        static void ExportCustomersCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("客户编号,客户名称,联系人,联系电话,开户行,银行账号,开户行行号,地址,实时当前应收款,状态,最后更新,操作人");
            foreach (var x in LoadCustomers()) sb.AppendLine(string.Join(",", new[] { x.Code,x.Company,x.Contact,x.Phone,x.Bank,x.Account,x.BankNo,x.Address,x.Receivable.ToString("0.00"),x.Status,x.UpdatedAt,x.UpdatedBy }.Select(Csv)));
            WriteCsvDownload(ctx, BuildExportFileName("客户管理"), sb.ToString());
        }

        static void ExportMaterialsCsv(HttpListenerContext ctx)
        {
            var sb=new StringBuilder();sb.AppendLine("物料编号,供应商,物料名称/规格,基准数量,单位,价格类型,单价,库存类型,是否纳入库存,是否成品,是否维修备件,安全库存,默认仓库,成本方式,备注,状态,最后更新,操作人");
            foreach(var x in LoadMaterials()){
                var parts=ParseMaterialQtyUnitParts(x.QuantityUnit);
                sb.AppendLine(string.Join(",",new[]{x.Code,x.Supplier,x.NameSpec,parts.BaseQty.ToString("0.##"),parts.Unit,NormalizePriceType(x.PriceType),MaterialDisplayUnitPrice(x).ToString("0.00"),x.StockType,(x.IsInventoryItem??true)?"是":"否",x.IsFinishedGood?"是":"否",x.IsServicePart?"是":"否",x.SafetyStock.ToString("0.##"),x.DefaultWarehouse,x.CostMethod,x.Note,x.Status,x.UpdatedAt,x.UpdatedBy}.Select(Csv)));
            }
            WriteCsvDownload(ctx, BuildExportFileName("物料管理"), sb.ToString());
        }

        static List<ContractSetting> LoadContractSettings()
        {
            lock (DataLock) return ReadJsonListCore<ContractSetting>(ContractSettingsFile);
        }

        static void SaveContractSettings(List<ContractSetting> items)
        {
            lock (DataLock) WriteJsonListCore(ContractSettingsFile, "contract_settings", items);
        }

        static List<ContractItem> LoadContracts()
        {
            lock (DataLock) return ReadJsonListCore<ContractItem>(ContractsFile);
        }

        static void SaveContracts(List<ContractItem> items)
        {
            lock (DataLock) WriteJsonListCore(ContractsFile, "contracts", items);
        }

        static string NowTimeString() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); }

        static decimal RoundMoney(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }

        static string ToChineseMoney(decimal amount)
        {
            if (amount == 0) return "零元整";
            string[] digits = { "零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖" };
            string[] units = { "", "拾", "佰", "仟" };
            string[] bigUnits = { "", "万", "亿", "兆" };
            bool negative = amount < 0;
            amount = Math.Abs(amount);
            long cents = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
            long intPart = cents / 100;
            int jiao = (int)(cents % 100 / 10);
            int fen = (int)(cents % 10);
            var sb = new StringBuilder();
            if (negative) sb.Append("负");
            if (intPart == 0) sb.Append("零");
            else
            {
                string intStr = intPart.ToString();
                int len = intStr.Length;
                for (int i = 0; i < len; i++)
                {
                    int digit = intStr[i] - '0';
                    int pos = len - i - 1;
                    int unitPos = pos % 4;
                    int bigUnitPos = pos / 4;
                    if (digit == 0)
                    {
                        if (unitPos == 0 && bigUnitPos > 0) sb.Append(bigUnits[bigUnitPos]);
                        else if (i < len - 1 && intStr[i + 1] != '0' && sb.Length > 0 && sb[sb.Length - 1] != '零') sb.Append("零");
                    }
                    else
                    {
                        sb.Append(digits[digit]).Append(units[unitPos]);
                        if (unitPos == 0 && bigUnitPos > 0) sb.Append(bigUnits[bigUnitPos]);
                    }
                }
            }
            sb.Append("元");
            if (jiao == 0 && fen == 0) sb.Append("整");
            else
            {
                if (jiao > 0) sb.Append(digits[jiao]).Append("角");
                else if (fen > 0) sb.Append("零");
                if (fen > 0) sb.Append(digits[fen]).Append("分");
            }
            return sb.ToString();
        }

        static void RecalcContractLine(ContractDetailLine line)
        {
            if (line.TaxIncluded && line.TaxRate >= 0)
                line.NoTaxUnitPrice = Math.Round(line.UnitPrice / (1 + line.TaxRate / 100m), 4, MidpointRounding.AwayFromZero);
            else
                line.NoTaxUnitPrice = line.UnitPrice;
            // 销售合同统一优先按不含税金额核算；旧合同的含税单价仍可兼容换算。
            line.TotalAmount = RoundMoney(line.Quantity * line.NoTaxUnitPrice);
        }

        static void RecalcContractAmounts(ContractItem item)
        {
            item.Items = item.Items ?? new List<ContractDetailLine>();
            decimal total = 0;
            int seq = 1;
            foreach (var line in item.Items)
            {
                line.Seq = seq++;
                RecalcContractLine(line);
                total += line.TotalAmount;
            }
            item.TotalAmount = RoundMoney(total);
            item.TotalAmountChinese = ToChineseMoney(item.TotalAmount);
            item.DepositAmount = RoundMoney(item.TotalAmount * item.DepositRatio / 100m);
            item.DepositAmountChinese = ToChineseMoney(item.DepositAmount);
            item.BalanceAmount = RoundMoney(item.TotalAmount - item.DepositAmount);
            item.BalanceAmountChinese = ToChineseMoney(item.BalanceAmount);
            if (item.InstallmentMonths > 0)
            {
                item.InstallmentAmount = RoundMoney(item.BalanceAmount / item.InstallmentMonths);
                item.InstallmentAmountChinese = ToChineseMoney(item.InstallmentAmount);
            }
            else
            {
                item.InstallmentAmount = 0;
                item.InstallmentAmountChinese = "";
            }
        }

        static void ApplyTemplateSnapshot(ContractItem item)
        {
            var settings = LoadContractSettings();
            ContractSetting tpl = null;
            if (!string.IsNullOrWhiteSpace(item.TemplateId))
                tpl = settings.FirstOrDefault(x => x.Id == item.TemplateId);
            if (tpl == null)
                tpl = settings.FirstOrDefault(x => x.Type == "合同范本" && x.IsDefault && x.Status == "启用");
            if (tpl == null)
                tpl = settings.FirstOrDefault(x => x.Type == "合同范本" && x.Status == "启用");
            if (tpl != null)
            {
                item.TemplateId = tpl.Id;
                item.TemplateCode = tpl.Code;
                item.TemplateName = tpl.Name;
                item.TemplateContent = tpl.Content;
            }
            else if (string.IsNullOrWhiteSpace(item.TemplateContent))
            {
                item.TemplateName = "冠誉公司自用销售合同默认范本";
                item.TemplateContent = GetDefaultContractTemplateHtml();
            }
        }

        static void NormalizeContract(ContractItem item)
        {
            item.Items = item.Items ?? new List<ContractDetailLine>();
            item.Accessories = item.Accessories ?? new List<ContractAccessoryLine>();
            item.ConfigItems = item.ConfigItems ?? new List<ContractConfigLine>();
            item.TechParams = item.TechParams ?? new List<ContractTechParamLine>();
            if (string.IsNullOrWhiteSpace(item.PartyBName)) item.PartyBName = "中山市冠誉数控设备有限公司";
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "草稿";
            int aSeq = 1;
            foreach (var a in item.Accessories) a.Seq = aSeq++;
            int cSeq = 1;
            foreach (var c in item.ConfigItems) c.Seq = cSeq++;
            int tSeq = 1;
            foreach (var t in item.TechParams) t.Seq = tSeq++;
            RecalcContractAmounts(item);
            ApplyTemplateSnapshot(item);
        }

        static void ValidateContractSetting(ContractSetting item)
        {
            if (string.IsNullOrWhiteSpace(item.Type)) throw new Exception("资料类型不能为空");
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("资料名称不能为空");
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "启用";
        }

        static void AddContractSetting(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ContractSetting>(ReadBody(ctx.Request));
            ValidateContractSetting(item);
            string now = BizUpdatedAtNow();
            var saved = MutateJsonList<ContractSetting, ContractSetting>(ContractSettingsFile, "contract_settings", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(ContractSettingSequenceFile, "CS", list.Select(x => x.Code));
                item.CreatedAt = now;
                item.UpdatedAt = now;
                if (item.IsDefault)
                    foreach (var x in list.Where(x => x.Type == item.Type)) x.IsDefault = false;
                list.Insert(0, item);
                return new JsonMutationResult<ContractSetting>(item, true);
            });
            Audit(user, "新增合同资料", saved.Code + " " + saved.Name);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateContractSetting(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ContractSetting>(ReadBody(ctx.Request));
            ValidateContractSetting(input);
            var saved = MutateJsonList<ContractSetting, ContractSetting>(ContractSettingsFile, "contract_settings", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同资料不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                input.Id = item.Id;
                input.Code = item.Code;
                input.CreatedAt = item.CreatedAt;
                input.UpdatedAt = BizUpdatedAtNow();
                if (input.IsDefault)
                    foreach (var x in list.Where(x => x.Type == input.Type && x.Id != id)) x.IsDefault = false;
                list[list.IndexOf(item)] = input;
                return new JsonMutationResult<ContractSetting>(input, true);
            });
            Audit(user, "修改合同资料", saved.Code + " " + saved.Name);
            WriteJson(ctx, saved);
        }

        static void DeleteContractSetting(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<ContractSetting>(ContractSettingsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同资料不存在", 404);
                auditDetail = item.Code + " " + item.Name;
            });
            EnforceDeleteImpact("contractSetting", id, user, ctx, auditDetail);
            MutateJsonList<ContractSetting, object>(ContractSettingsFile, "contract_settings", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同资料不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除合同资料", auditDetail);
            WriteJson(ctx, new { ok = true });
        }

        static void AddContract(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ContractItem>(ReadBody(ctx.Request));
            if (string.IsNullOrWhiteSpace(item.Name)) throw new Exception("合同名称不能为空");
            if (string.IsNullOrWhiteSpace(item.PartyAName)) throw new Exception("甲方名称不能为空");
            if (item.Items == null || item.Items.Count == 0) throw new Exception("请至少添加一条设备明细");
            NormalizeContract(item);
            string now = BizUpdatedAtNow();
            var saved = MutateJsonList<ContractItem, ContractItem>(ContractsFile, "contracts", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(ContractSequenceFile, "CON", list.Select(x => x.Code), "HT");
                item.CreatedAt = now;
                item.UpdatedAt = now;
                list.Insert(0, item);
                return new JsonMutationResult<ContractItem>(item, true);
            });
            Audit(user, "新增合同", saved.Code + " " + saved.Name);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateContract(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ContractItem>(ReadBody(ctx.Request));
            if (string.IsNullOrWhiteSpace(input.Name)) throw new Exception("合同名称不能为空");
            if (string.IsNullOrWhiteSpace(input.PartyAName)) throw new Exception("甲方名称不能为空");
            if (input.Items == null || input.Items.Count == 0) throw new Exception("请至少添加一条设备明细");
            var listSnapshot = LoadContracts();
            var existing = listSnapshot.FirstOrDefault(x => x.Id == id);
            if (existing == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            if (string.IsNullOrWhiteSpace(input.TemplateContent)) input.TemplateContent = existing.TemplateContent;
            NormalizeContract(input);
            var saved = MutateJsonList<ContractItem, ContractItem>(ContractsFile, "contracts", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                input.Id = item.Id;
                input.Code = item.Code;
                input.CreatedAt = item.CreatedAt;
                input.UpdatedAt = BizUpdatedAtNow();
                list[list.IndexOf(item)] = input;
                return new JsonMutationResult<ContractItem>(input, true);
            });
            Audit(user, "修改合同", saved.Code + " " + saved.Name);
            WriteJson(ctx, saved);
        }

        static void DeleteContract(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditDetail = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<ContractItem>(ContractsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同不存在", 404);
                auditDetail = item.Code + " " + item.Name;
            });
            EnforceDeleteImpact("contract", id, user, ctx, auditDetail);
            MutateJsonList<ContractItem, object>(ContractsFile, "contracts", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除合同", auditDetail);
            WriteJson(ctx, new { ok = true });
        }

        static void VoidContract(HttpListenerContext ctx, UserSession user, string id)
        {
            string clientUpdatedAt = "";
            try
            {
                var raw = ReadBody(ctx.Request);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var req = Json.Deserialize<Dictionary<string, string>>(raw);
                    if (req != null && req.TryGetValue("UpdatedAt", out var ua))
                        clientUpdatedAt = ua ?? "";
                }
            }
            catch { }
            var saved = MutateJsonList<ContractItem, ContractItem>(ContractsFile, "contracts", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("合同不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, clientUpdatedAt);
                item.Status = "已作废";
                item.UpdatedAt = BizUpdatedAtNow();
                return new JsonMutationResult<ContractItem>(item, true);
            });
            Audit(user, "作废合同", saved.Code + " " + saved.Name);
            WriteJson(ctx, saved);
        }

        static void PreviewContract(HttpListenerContext ctx, string id)
        {
            var item = LoadContracts().FirstOrDefault(x => x.Id == id);
            if (item == null) { WriteJson(ctx, new { error = "合同不存在" }, 404); return; }
            // 预览始终重新按不含税口径计算，兼容历史合同中保存的含税合计。
            RecalcContractAmounts(item);
            string html = BuildContractPreviewHtml(item);
            WriteJson(ctx, new ContractPreviewResult { Html = html });
        }

        static string BuildContractPreviewHtml(ContractItem item)
        {
            string template = item.TemplateContent;
            if (string.IsNullOrWhiteSpace(template) || IsLegacyDefaultContractTemplate(item.TemplateName, template))
                template = GetDefaultContractTemplateHtml();
            var map = new Dictionary<string, string>
            {
                ["{{合同编号}}"] = item.Code ?? "",
                ["{{合同名称}}"] = item.Name ?? "",
                ["{{签订日期}}"] = item.SignDate ?? "",
                ["{{甲方名称}}"] = item.PartyAName ?? "",
                ["{{甲方联系人}}"] = item.PartyAContact ?? "",
                ["{{甲方电话}}"] = item.PartyAPhone ?? "",
                ["{{甲方地址}}"] = item.PartyAAddress ?? "",
                ["{{乙方名称}}"] = item.PartyBName ?? "",
                ["{{乙方联系人}}"] = item.PartyBContact ?? "",
                ["{{乙方电话}}"] = item.PartyBPhone ?? "",
                ["{{乙方地址}}"] = item.PartyBAddress ?? "",
                ["{{设备明细}}"] = BuildDeviceDetailTable(item.Items),
                ["{{随机配件}}"] = BuildAccessoryTable(item.Accessories),
                ["{{付款方式}}"] = (item.PaymentTerms ?? "").Replace("\n", "<br>"),
                ["{{定金金额}}"] = item.DepositAmount.ToString("0.00"),
                ["{{定金金额大写}}"] = item.DepositAmountChinese ?? "",
                ["{{余款金额}}"] = item.BalanceAmount.ToString("0.00"),
                ["{{余款金额大写}}"] = item.BalanceAmountChinese ?? "",
                ["{{分期说明}}"] = (item.InstallmentNote ?? "").Replace("\n", "<br>"),
                ["{{交货时间}}"] = item.DeliveryTime ?? "",
                ["{{交货地点}}"] = item.DeliveryPlace ?? "",
                ["{{包装方式}}"] = item.PackagingMethod ?? "",
                ["{{运输方式}}"] = item.TransportMethod ?? "",
                ["{{质量验收条款}}"] = (item.QualityAcceptanceTerms ?? "").Replace("\n", "<br>"),
                ["{{售后维修条款}}"] = (item.AfterSalesTerms ?? "").Replace("\n", "<br>"),
                ["{{不保修范围}}"] = (item.ExcludedWarranty ?? "").Replace("\n", "<br>"),
                ["{{违约责任}}"] = (item.BreachTerms ?? "").Replace("\n", "<br>"),
                ["{{合同总金额}}"] = item.TotalAmount.ToString("0.00"),
                ["{{合同总金额大写}}"] = item.TotalAmountChinese ?? "",
                ["{{是否含税}}"] = item.TaxIncluded ? "含税" : "不含税",
                ["{{税率}}"] = item.TaxRate.ToString("0.##") + "%",
                ["{{开票类型}}"] = item.InvoiceType ?? "",
                ["{{备注}}"] = (item.InternalNote ?? "").Replace("\n", "<br>"),
                ["{{开票名称}}"] = item.InvoiceTitle ?? "",
                ["{{纳税人识别号}}"] = item.TaxNumber ?? "",
                ["{{开户名称}}"] = item.AccountHolder ?? "",
                ["{{开户行}}"] = item.BankBranch ?? "",
                ["{{公司账号}}"] = item.CompanyAccount ?? "",
                ["{{私人账号}}"] = item.PersonalAccount ?? "",
                ["{{行号}}"] = item.BankRoutingNo ?? "",
                ["{{设备配置表}}"] = BuildConfigTable(item.ConfigItems),
                ["{{技术参数表}}"] = BuildTechParamTable(item.TechParams),
                ["{{甲方签字}}"] = "________________",
                ["{{乙方签字}}"] = "________________",
                ["{{甲方签约日期}}"] = item.SignDate ?? "",
                ["{{乙方签约日期}}"] = item.SignDate ?? ""
            };
            var html = template;
            foreach (var kv in map) html = html.Replace(kv.Key, kv.Value);
            if (!html.Contains("class=\"contract-document\"") && !html.Contains("class='contract-document'"))
                html = "<article class=\"contract-document\">" + html + "</article>";
            return html;
        }

        static string BuildDeviceDetailTable(List<ContractDetailLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table class=\"device-detail-table\"><thead><tr><th>设备名称</th><th>型号规格</th><th>数量/单位</th><th>单价/不含税</th><th>合计金额/不含税</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(HtmlEncode(x.DeviceName)).Append("</td><td>").Append(HtmlEncode(x.ModelSpec)).Append("</td><td>").Append(x.Quantity.ToString("0.##")).Append("/").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(x.NoTaxUnitPrice.ToString("0.00")).Append("</td><td>").Append(x.TotalAmount.ToString("0.00")).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static bool IsLegacyDefaultContractTemplate(string templateName, string content)
        {
            if (!string.Equals(templateName, "冠誉设备购销合同默认范本", StringComparison.OrdinalIgnoreCase)) return false;
            return string.IsNullOrWhiteSpace(content) || content.Contains("签订日期：{{签订日期}}。甲乙双方经友好协商");
        }

        static string BuildAccessoryTable(List<ContractAccessoryLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>配件名称</th><th>规格型号</th><th>数量</th><th>单位</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Name)).Append("</td><td>").Append(HtmlEncode(x.Spec)).Append("</td><td>").Append(HtmlEncode(x.Quantity)).Append("</td><td>").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string BuildConfigTable(List<ContractConfigLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>分类</th><th>项目</th><th>产品/品牌</th><th>规格型号</th><th>数量</th><th>单位</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Category)).Append("</td><td>").Append(HtmlEncode(x.Item)).Append("</td><td>").Append(HtmlEncode(x.Brand)).Append("</td><td>").Append(HtmlEncode(x.Spec)).Append("</td><td>").Append(HtmlEncode(x.Quantity)).Append("</td><td>").Append(HtmlEncode(x.Unit)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string BuildTechParamTable(List<ContractTechParamLine> items)
        {
            if (items == null || items.Count == 0) return "<p>无</p>";
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr><th>序号</th><th>分类</th><th>名称</th><th>规格/型号/数值</th><th>备注</th></tr></thead><tbody>");
            foreach (var x in items)
                sb.Append("<tr><td>").Append(x.Seq).Append("</td><td>").Append(HtmlEncode(x.Category)).Append("</td><td>").Append(HtmlEncode(x.Name)).Append("</td><td>").Append(HtmlEncode(x.Value)).Append("</td><td>").Append(HtmlEncode(x.Note)).Append("</td></tr>");
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        static void EnsureDefaultContractSettings()
        {
            var list = LoadContractSettings();
            var legacyDefault = list.FirstOrDefault(x => x.Type == "合同范本" && x.Name == "冠誉设备购销合同默认范本");
            if (legacyDefault != null && IsLegacyDefaultContractTemplate(legacyDefault.Name, legacyDefault.Content))
            {
                legacyDefault.Name = "冠誉公司自用销售合同默认范本";
                legacyDefault.Content = GetDefaultContractTemplateHtml();
                legacyDefault.Note = "系统预置公司自用销售合同 HTML 范本（不含税）";
                legacyDefault.UpdatedAt = NowTimeString();
                SaveContractSettings(list);
                return;
            }
            if (list.Any(x => x.Type == "合同范本")) return;
            string now = NowTimeString();
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS01",
                Type = "合同范本",
                Name = "冠誉公司自用销售合同默认范本",
                Content = GetDefaultContractTemplateHtml(),
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                Note = "系统预置公司自用销售合同 HTML 范本（不含税）",
                CreatedAt = now,
                UpdatedAt = now
            });
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS02",
                Type = "付款方式",
                Name = "标准付款方式（30%定金+5期余款）",
                Content = "1、定金：合同签订后甲方支付合同总金额定金30%。\n2、余款：合同余款分五个月等额支付。\n3、所有权保留：甲方付清全部货款前，设备所有权归乙方所有，甲方仅享有使用权。",
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            list.Add(new ContractSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = "CS03",
                Type = "公司资料",
                Name = "冠誉公司资料",
                Content = "中山市冠誉数控设备有限公司\n地址：广东省中山市\n联系人：王浪\n电话：18988541298",
                IsDefault = true,
                Status = "启用",
                Sort = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            if (!File.Exists(ContractSettingSequenceFile) || File.ReadAllText(ContractSettingSequenceFile).Trim() == "0")
                File.WriteAllText(ContractSettingSequenceFile, "3", new UTF8Encoding(false));
            SaveContractSettings(list);
        }

        static string GetDefaultContractTemplateHtml()
        {
            return @"<article class=""contract-document"">
<div class=""ct-header"">专业设计制造销售：数控车床、车铣复合车床、双主轴数控车床、自动化方案定制-王浪 18988541298</div>
<p class=""ct-contract-no"">合同编号：{{合同编号}}</p>
<h2>中山市冠誉数控设备有限公司销售合同</h2>
<div class=""ct-parties""><p>甲方（需方）：{{甲方名称}}</p><p>乙方（供方）：中山市冠誉数控设备有限公司</p></div>
<p class=""ct-intro"">甲乙双方本着平等互利、诚实信用的原则，就甲方向乙方购买设备事宜，经友好协商，达成如下协议</p>
<h3>一、合同主体与签订背景</h3>
<p>甲乙双方经友好协商，就设备购销事宜达成一致，旨在明确双方的权利、义务和责任，确保交易的顺利进行，特签订以下合同。</p>
<h3>二、设备名称、规格、数量、单价</h3>
{{设备明细}}
<div class=""ct-amount-lines""><p>小写（不含税）：¥{{合同总金额}}元</p><p>大写（不含税）：{{合同总金额大写}}</p></div>
<h3>三、随机配件</h3>
{{随机配件}}
<h3>四、付款方式与期限</h3>
<p>{{付款方式}}</p>
<p>定金：{{定金金额}} 元（大写：{{定金金额大写}}）；余款：{{余款金额}} 元（大写：{{余款金额大写}}）。{{分期说明}}</p>
<h3>五、交货时间、地点、包装及运输</h3>
<p>交货时间：{{交货时间}}；交货地点：{{交货地点}}；包装方式：{{包装方式}}；运输方式：{{运输方式}}。</p>
<h3>六、质量标准与检验验收</h3>
<p>{{质量验收条款}}</p>
<h3>七、售后维修</h3>
<p>{{售后维修条款}}</p>
<h3>八、不保修范围</h3>
<p>{{不保修范围}}</p>
<h3>九、违约责任</h3>
<p>{{违约责任}}</p>
<h3>十、合同生效</h3>
<p>本合同一式贰份，甲乙双方各执壹份，自双方签字盖章之日起生效。</p>
<table style=""width:100%;margin-top:30px""><tr><td>甲方签字/盖章：{{甲方签字}}<br>日期：{{甲方签约日期}}</td><td>乙方签字/盖章：{{乙方签字}}<br>日期：{{乙方签约日期}}</td></tr></table>
<h3>十一、开票信息及收款账户</h3>
<p>开票名称：{{开票名称}} &nbsp; 纳税人识别号：{{纳税人识别号}}<br>
开户名称：{{开户名称}} &nbsp; 开户行：{{开户行}}<br>
公司账号：{{公司账号}} &nbsp; 私人账号：{{私人账号}} &nbsp; 行号：{{行号}}</p>
<h3>十二、设备配置表</h3>
{{设备配置表}}
<h3>十三、技术参数表</h3>
{{技术参数表}}
<p style=""margin-top:20px"">备注：{{备注}}</p>
</article>";
        }

        static void EnsureBusinessDataFiles()
        {
            EnsureJsonFile(SalesOrdersFile);
            EnsureJsonFile(SalesOrderSequenceFile, "0");
            EnsureJsonFile(SalesOutboundsFile);
            EnsureJsonFile(SalesOutboundSequenceFile, "0");
            EnsureJsonFile(PurchaseOrdersFile);
            EnsureJsonFile(PurchaseOrderSequenceFile, "0");
            EnsureJsonFile(PurchaseInboundsFile);
            EnsureJsonFile(PurchaseInboundSequenceFile, "0");
            EnsureJsonFile(ProductionPicksFile);
            EnsureJsonFile(ProductionPickSequenceFile, "0");
            EnsureJsonFile(FinishedInboundsFile);
            EnsureJsonFile(FinishedInboundSequenceFile, "0");
            EnsureJsonFile(ProductionWorkOrdersFile);
            EnsureJsonFile(AfterSalesServiceOrdersFile);
            EnsureJsonFile(ReceivablesFile);
            EnsureJsonFile(ReceivableSequenceFile, "0");
            EnsureJsonFile(PayablesFile);
            EnsureJsonFile(PayableSequenceFile, "0");
        }

        static void EnsureJsonFile(string path, string defaultContent = "[]")
        {
            if (!File.Exists(path)) File.WriteAllText(path, defaultContent, new UTF8Encoding(false));
        }

        static string TodayText() { return DateTime.Now.ToString("yyyy-MM-dd"); }

        static bool IsConfirmedStatus(string status) { return string.Equals(status ?? "", "已确认", StringComparison.OrdinalIgnoreCase); }

        static string NormalizeDocStatus(string status)
        {
            status = (status ?? "").Trim();
            if (status == "已确认" || status == "草稿" || status == "已取消") return status;
            return "草稿";
        }

        static string NormalizeReceivableStatus(decimal receivable, decimal received)
        {
            if (received <= 0) return "未收款";
            if (received >= receivable) return "已收清";
            return "部分收款";
        }

        static string NormalizePayableStatus(decimal payable, decimal paid)
        {
            if (paid <= 0) return "未付款";
            if (paid >= payable) return "已付清";
            return "部分付款";
        }

        static void BizFail(string message, int statusCode = 400) { throw new BusinessException(message, statusCode); }

        static Material FindMaterialByIdOrName(string materialId, string materialName)
        {
            var materials = LoadMaterials();
            if (!string.IsNullOrWhiteSpace(materialId))
            {
                var byId = materials.FirstOrDefault(x => x.Id == materialId);
                if (byId != null) return byId;
            }
            if (!string.IsNullOrWhiteSpace(materialName))
            {
                var byCode = materials.FirstOrDefault(x => string.Equals(x.Code, materialName, StringComparison.OrdinalIgnoreCase));
                if (byCode != null) return byCode;
                return materials.FirstOrDefault(x => string.Equals(x.NameSpec, materialName, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }

        static void ResolveMaterialFields(string materialId, string materialName, out string id, out string code, out string name, out string spec, out string unit)
        {
            var material = FindMaterialByIdOrName(materialId, materialName);
            if (material != null)
            {
                id = material.Id;
                code = material.Code ?? "";
                name = material.NameSpec ?? "";
                spec = material.NameSpec ?? "";
                unit = material.QuantityUnit ?? "";
                return;
            }
            id = (materialId ?? "").Trim();
            code = "";
            name = (materialName ?? "").Trim();
            spec = name;
            unit = "";
        }

        static void ResolveSalesOrderLink(SalesOutbound item)
        {
            item.SalesOrderId = (item.SalesOrderId ?? "").Trim();
            item.SalesOrderNo = (item.SalesOrderNo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.SalesOrderId) && string.IsNullOrWhiteSpace(item.SalesOrderNo))
                BizFail("请选择来源销售订单");
            var orders = LoadSalesOrders();
            SalesOrder order = null;
            if (!string.IsNullOrWhiteSpace(item.SalesOrderId))
                order = orders.FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null && !string.IsNullOrWhiteSpace(item.SalesOrderNo))
                order = orders.FirstOrDefault(x => string.Equals(x.Code, item.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            if (order == null) BizFail("来源销售订单不存在，请先在销售订单中创建");
            item.SalesOrderId = order.Id;
            item.SalesOrderNo = order.Code ?? "";
            item.CustomerName = order.CustomerName ?? "";
            item.ItemType = NormalizeSalesItemType(order.ItemType);
            item.ModelCostId = order.ModelCostId ?? "";
            item.BomId = order.BomId ?? "";
            if (item.ItemType == "FinishedProduct")
            {
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
                if (mc != null)
                {
                    item.BomId = mc.BomId ?? item.BomId;
                    item.BomCode = mc.BomCode ?? "";
                    item.MaterialCode = mc.ModelCode ?? "";
                    item.MaterialName = !string.IsNullOrWhiteSpace(mc.ProductName) ? mc.ProductName : mc.ModelName;
                }
                else
                {
                    item.MaterialCode = order.MaterialCode ?? "";
                    item.MaterialName = order.MaterialName ?? "";
                }
                item.MaterialId = "";
            }
            else
            {
                item.MaterialId = order.MaterialId ?? "";
                item.MaterialCode = order.MaterialCode ?? "";
                item.MaterialName = order.MaterialName ?? "";
            }
            if (item.Quantity <= 0) item.Quantity = order.Quantity;
        }

        static void ApplySalesOutboundItemFields(SalesOutbound item)
        {
            item.ItemType = NormalizeSalesItemType(item.ItemType);
            item.ModelCostId = (item.ModelCostId ?? "").Trim();
            item.BomId = (item.BomId ?? "").Trim();
            item.BomCode = (item.BomCode ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(item.ModelCostId)) item.ItemType = "FinishedProduct";
            if (item.ItemType == "FinishedProduct" && !string.IsNullOrWhiteSpace(item.ModelCostId))
            {
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
                if (mc != null)
                {
                    item.BomId = mc.BomId ?? item.BomId;
                    item.BomCode = mc.BomCode ?? item.BomCode;
                    if (string.IsNullOrWhiteSpace(item.MaterialName))
                        item.MaterialName = !string.IsNullOrWhiteSpace(mc.ProductName) ? mc.ProductName : mc.ModelName;
                    if (string.IsNullOrWhiteSpace(item.MaterialCode)) item.MaterialCode = mc.ModelCode ?? "";
                }
                item.MaterialId = "";
            }
        }

        static void ResolvePurchaseOrderLink(PurchaseInbound item)
        {
            item.PurchaseOrderId = (item.PurchaseOrderId ?? "").Trim();
            item.PurchaseNo = (item.PurchaseNo ?? "").Trim();
            item.SupplierName = (item.SupplierName ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(item.PurchaseOrderId))
            {
                var order = LoadPurchaseOrders().FirstOrDefault(x => x.Id == item.PurchaseOrderId);
                if (order == null) BizFail("来源采购单不存在，请先在采购单中创建");
                item.PurchaseNo = order.Code ?? "";
                item.SupplierName = order.SupplierName ?? "";
                item.MaterialId = order.MaterialId ?? "";
                item.MaterialCode = order.MaterialCode ?? "";
                item.MaterialName = order.MaterialName ?? "";
                if (item.InboundPrice <= 0) item.InboundPrice = order.UnitPrice;
                return;
            }
            item.PurchaseNo = "";
            if (string.IsNullOrWhiteSpace(item.SupplierName)) BizFail("请选择供应商或来源采购单");
            if (string.IsNullOrWhiteSpace(item.MaterialId) && string.IsNullOrWhiteSpace(item.MaterialName))
                BizFail("请选择物料");
            if (item.InboundPrice <= 0 && !string.IsNullOrWhiteSpace(item.MaterialId))
            {
                var material = LoadMaterials().FirstOrDefault(x => x.Id == item.MaterialId);
                if (material != null) item.InboundPrice = MaterialDisplayUnitPrice(material);
            }
        }

        static void ResolveBomLinkForPick(ProductionPick item)
        {
            if (string.IsNullOrWhiteSpace(item.BomId)) return;
            var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
            if (bom == null) return;
            item.BomName = string.IsNullOrWhiteSpace(bom.ModelName) ? bom.ProductName : bom.ModelName;
        }

        static void ResolveBomAndModelCostLink(FinishedInbound item)
        {
            if (!string.IsNullOrWhiteSpace(item.ModelCostId))
            {
                var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
                if (mc != null)
                {
                    if (string.IsNullOrWhiteSpace(item.BomId)) item.BomId = mc.BomId;
                    if (string.IsNullOrWhiteSpace(item.BomCode)) item.BomCode = mc.BomCode;
                    if (string.IsNullOrWhiteSpace(item.ProductName)) item.ProductName = mc.ProductName ?? mc.ModelName;
                }
            }
            if (!string.IsNullOrWhiteSpace(item.BomId))
            {
                var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
                if (bom != null)
                {
                    item.BomCode = bom.Code;
                    if (string.IsNullOrWhiteSpace(item.ProductName)) item.ProductName = bom.ProductName ?? bom.ModelName;
                }
            }
        }

        static void ResolveReceivableSalesOrderLink(Receivable item)
        {
            if (string.IsNullOrWhiteSpace(item.SalesOrderId)) return;
            var order = LoadSalesOrders().FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null) return;
            item.SalesOrderNo = order.Code;
            if (string.IsNullOrWhiteSpace(item.CustomerName)) item.CustomerName = order.CustomerName;
            if (item.ReceivableAmount <= 0 && order.Amount > 0) item.ReceivableAmount = order.Amount;
        }

        static void ResolvePayablePurchaseOrderLink(Payable item)
        {
            if (string.IsNullOrWhiteSpace(item.PurchaseOrderId)) return;
            var order = LoadPurchaseOrders().FirstOrDefault(x => x.Id == item.PurchaseOrderId);
            if (order == null) return;
            item.PurchaseNo = order.Code;
            if (string.IsNullOrWhiteSpace(item.SupplierName)) item.SupplierName = order.SupplierName;
            if (item.PayableAmount <= 0 && order.Amount > 0) item.PayableAmount = order.Amount;
        }

        static List<T> LoadJsonList<T>(string file)
        {
            lock (DataLock) return ReadJsonListCore<T>(file);
        }

        static void SaveJsonList<T>(string file, string backupPrefix, List<T> items)
        {
            lock (DataLock) WriteJsonListCore(file, backupPrefix, items);
        }

        static readonly string[] SalesOrderStringFields = { "CustomerId", "CustomerCode", "CustomerName", "CustomerContact", "CustomerPhone", "CustomerAddress", "MaterialId", "MaterialCode", "MaterialName", "OrderDate", "Status", "Note", "Code", "Id" };

        static void CoerceJsonStringFields(JsonObject obj, params string[] keys)
        {
            if (obj == null) return;
            foreach (var key in keys)
            {
                if (!obj.TryGetPropertyValue(key, out var node) || node == null) continue;
                if (node is JsonObject || node is JsonArray) { obj[key] = node.ToJsonString(); continue; }
                if (node is JsonValue val)
                {
                    var el = val.GetValue<JsonElement>();
                    if (el.ValueKind == JsonValueKind.Number) obj[key] = el.GetRawText();
                    else if (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False) obj[key] = el.GetBoolean().ToString();
                    else if (el.ValueKind == JsonValueKind.Null) obj[key] = "";
                }
            }
        }

        static SalesOrder DeserializeSalesOrder(string json)
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj) CoerceJsonStringFields(obj, SalesOrderStringFields);
            return Json.Deserialize<SalesOrder>(node.ToJsonString());
        }

        static BatchSalesOrderRequest DeserializeBatchSalesOrderRequest(string json)
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject root && root.TryGetPropertyValue("Items", out var itemsNode) && itemsNode is JsonArray arr)
                foreach (var item in arr) if (item is JsonObject itemObj) CoerceJsonStringFields(itemObj, SalesOrderStringFields);
            return Json.Deserialize<BatchSalesOrderRequest>(node.ToJsonString());
        }

        static string BizUpdatedAtNow()
        {
            return DateTimeOffset.UtcNow.ToString("O");
        }

        static List<SalesOrder> LoadSalesOrders() { return LoadJsonList<SalesOrder>(SalesOrdersFile); }
        static void SaveSalesOrders(List<SalesOrder> items) { SaveJsonList(SalesOrdersFile, "sales_orders", items); }

        static void ResolveCustomerFields(SalesOrder item)
        {
            var customers = LoadCustomers();
            Customer matched = null;
            item.CustomerId = (item.CustomerId ?? "").Trim();
            item.CustomerCode = (item.CustomerCode ?? "").Trim();
            item.CustomerName = (item.CustomerName ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(item.CustomerId))
                matched = customers.FirstOrDefault(x => x.Id == item.CustomerId);
            if (matched == null && !string.IsNullOrWhiteSpace(item.CustomerCode))
                matched = customers.FirstOrDefault(x => string.Equals(x.Code, item.CustomerCode, StringComparison.OrdinalIgnoreCase));
            if (matched == null && !string.IsNullOrWhiteSpace(item.CustomerName))
                matched = customers.FirstOrDefault(x => string.Equals(x.Company, item.CustomerName, StringComparison.OrdinalIgnoreCase));
            if (matched == null) BizFail("客户不存在，请先在客户管理中添加客户");
            item.CustomerId = matched.Id;
            item.CustomerCode = matched.Code ?? "";
            item.CustomerName = matched.Company ?? "";
            item.CustomerContact = matched.Contact ?? "";
            item.CustomerPhone = matched.Phone ?? "";
            item.CustomerAddress = matched.Address ?? "";
        }

        static string NormalizeSalesItemType(string itemType)
        {
            return string.Equals(itemType ?? "", "FinishedProduct", StringComparison.OrdinalIgnoreCase) ? "FinishedProduct" : "Material";
        }

        static bool IsFinishedProductOutbound(SalesOutbound item)
        {
            if (item == null) return false;
            if (string.Equals(NormalizeSalesItemType(item.ItemType), "FinishedProduct", StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrWhiteSpace(item.ModelCostId);
        }

        static string GetFinishedProductStockId(string modelCostId, string bomId)
        {
            if (!string.IsNullOrWhiteSpace(modelCostId)) return modelCostId.Trim();
            return (bomId ?? "").Trim();
        }

        static void ResolveSalesOrderProductFields(SalesOrder item)
        {
            item.ItemType = NormalizeSalesItemType(item.ItemType);
            item.ModelCostId = (item.ModelCostId ?? "").Trim();
            item.BomId = (item.BomId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(item.ModelCostId)) item.ItemType = "FinishedProduct";
            if (item.ItemType != "FinishedProduct") return;
            if (string.IsNullOrWhiteSpace(item.ModelCostId)) BizFail("销售成品时必须选择机型成本");
            var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
            if (mc == null) BizFail("所选机型成本不存在");
            if ((mc.Status ?? "启用") != "启用") BizFail("所选机型成本已停用，不能用于销售");
            item.BomId = mc.BomId ?? "";
            item.MaterialCode = mc.ModelCode ?? "";
            item.MaterialName = !string.IsNullOrWhiteSpace(mc.ProductName) ? mc.ProductName : mc.ModelName;
            item.MaterialId = "";
        }

        static void ApplySalesOrder(SalesOrder item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveCustomerFields(item);
            ResolveSalesOrderProductFields(item);
            if (item.ItemType == "FinishedProduct")
            {
                if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("请填写产品名称");
            }
            else
            {
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                item.MaterialId = mid; item.MaterialCode = mcode; item.MaterialName = mname;
                if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("请填写物料名称");
            }
            if (item.Quantity <= 0) BizFail("数量必须大于 0");
            if (item.TaxExcludedSalePrice <= 0 && item.TaxIncludedSalePrice <= 0 && item.UnitPrice > 0)
                item.TaxExcludedSalePrice = item.UnitPrice;
            if (item.TaxExcludedSalePrice < 0) BizFail("不含税销售单价不能为负数");
            if (item.TaxIncludedSalePrice < 0) BizFail("含税销售单价不能为负数");
            item.TaxExcludedSaleAmount = CalcLineAmount(item.Quantity, item.TaxExcludedSalePrice);
            item.TaxIncludedSaleAmount = CalcLineAmount(item.Quantity, item.TaxIncludedSalePrice);
            item.UnitPrice = item.TaxExcludedSalePrice;
            item.Amount = item.TaxExcludedSaleAmount > 0 ? item.TaxExcludedSaleAmount : item.TaxIncludedSaleAmount;
            item.OrderDate = string.IsNullOrWhiteSpace(item.OrderDate) ? TodayText() : item.OrderDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddSalesOrder(HttpListenerContext ctx, UserSession user)
        {
            var item = DeserializeSalesOrder(ReadBody(ctx.Request)); ApplySalesOrder(item);
            var saved = PersistSalesOrderAdd(item, user);
            Audit(user, "新增销售订单", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdateSalesOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = DeserializeSalesOrder(ReadBody(ctx.Request)); ApplySalesOrder(input);
            var saved = PersistSalesOrderUpdate(id, input, user);
            Audit(user, "修改销售订单", saved.Code); WriteJson(ctx, saved);
        }

        static void DeleteSalesOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<SalesOrder>(SalesOrdersFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售订单不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("salesOrder", id, user, ctx, auditCode);
            PersistSalesOrderDelete(id, user);
            Audit(user, "删除销售订单", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<SalesOutbound> LoadSalesOutbounds() { return LoadJsonList<SalesOutbound>(SalesOutboundsFile); }
        static void SaveSalesOutbounds(List<SalesOutbound> items) { SaveJsonList(SalesOutboundsFile, "sales_outbounds", items); }

        static void ApplySalesOutbound(SalesOutbound item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveSalesOrderLink(item);
            ApplySalesOutboundItemFields(item);
            item.CustomerName = (item.CustomerName ?? "").Trim();
            if (IsFinishedProductOutbound(item))
            {
                if (string.IsNullOrWhiteSpace(item.ModelCostId)) BizFail("销售成品出库必须关联机型成本");
                if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("来源销售订单缺少产品信息");
            }
            else
            {
                string mid, mcode, mname, mspec, munit;
                ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
                item.MaterialId = mid; item.MaterialCode = mcode; item.MaterialName = mname;
                if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("来源销售订单缺少物料信息");
            }
            if (item.Quantity <= 0) BizFail("出库数量必须大于 0");
            ValidateSalesOutboundRemainingQty(item);
            AutoResolveSalesOutboundCost(item);
            if (item.CostPrice < 0) BizFail("成本单价不能为负数");
            item.CostAmount = CalcLineAmount(item.Quantity, item.CostPrice);
            item.OutboundDate = string.IsNullOrWhiteSpace(item.OutboundDate) ? TodayText() : item.OutboundDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddSalesOutbound(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<SalesOutbound>(ReadBody(ctx.Request)); ApplySalesOutbound(item);
            ValidateStockForConfirmedOutbound(item);
            var saved = MutateJsonList<SalesOutbound, SalesOutbound>(SalesOutboundsFile, "sales_outbounds", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(SalesOutboundSequenceFile, "SOUT", list.Select(x => x.Code), "XSCK");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<SalesOutbound>(item, true);
            });
            Audit(user, "新增销售出库", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdateSalesOutbound(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<SalesOutbound>(ReadBody(ctx.Request)); ApplySalesOutbound(input);
            ValidateSalesOutboundRemainingQty(input, id);
            ValidateStockForConfirmedOutbound(input, id);
            var saved = MutateJsonList<SalesOutbound, SalesOutbound>(SalesOutboundsFile, "sales_outbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售出库不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureConfirmedInventoryDocEditBlocked(item.Status, input.Status, "销售出库");
                if (IsConfirmedStatus(item.Status) && (item.Quantity != input.Quantity
                    || !string.Equals(item.MaterialId ?? "", input.MaterialId ?? "", StringComparison.Ordinal)
                    || !string.Equals(item.ModelCostId ?? "", input.ModelCostId ?? "", StringComparison.Ordinal)
                    || !string.Equals(NormalizeSalesItemType(item.ItemType), NormalizeSalesItemType(input.ItemType), StringComparison.OrdinalIgnoreCase)))
                    BizFail(ReferenceLockMessage, 409);
                item.SalesOrderId = input.SalesOrderId; item.SalesOrderNo = input.SalesOrderNo;
                item.CustomerName = input.CustomerName;
                item.ItemType = input.ItemType; item.ModelCostId = input.ModelCostId;
                item.BomId = input.BomId; item.BomCode = input.BomCode;
                item.MaterialId = input.MaterialId; item.MaterialCode = input.MaterialCode;
                item.MaterialName = input.MaterialName; item.Quantity = input.Quantity;
                item.CostPrice = input.CostPrice; item.CostAmount = input.CostAmount; item.OutboundDate = input.OutboundDate;
                item.Status = input.Status; item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<SalesOutbound>(item, true);
            });
            Audit(user, "修改销售出库", saved.Code); WriteJson(ctx, saved);
        }

        static void DeleteSalesOutbound(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<SalesOutbound>(SalesOutboundsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售出库不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("salesOutbound", id, user, ctx, auditCode);
            MutateJsonList<SalesOutbound, object>(SalesOutboundsFile, "sales_outbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("销售出库不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除销售出库", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<PurchaseOrder> LoadPurchaseOrders() { return LoadJsonList<PurchaseOrder>(PurchaseOrdersFile); }
        static void SavePurchaseOrders(List<PurchaseOrder> items) { SaveJsonList(PurchaseOrdersFile, "purchase_orders", items); }

        static void ApplyPurchaseOrder(PurchaseOrder item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveSupplierFields(item);
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
            item.MaterialId = mid; item.MaterialCode = mcode; item.MaterialName = mname;
            if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("请选择物料");
            ApplyMaterialDefaultPriceToPurchaseOrder(item);
            item.PriceType = NormalizePriceType(item.PriceType);
            if (item.Quantity <= 0) BizFail("数量必须大于 0");
            if (item.UnitPrice < 0) BizFail("采购单价不能为负数");
            item.Amount = CalcLineAmount(item.Quantity, item.UnitPrice);
            item.OrderDate = string.IsNullOrWhiteSpace(item.OrderDate) ? TodayText() : item.OrderDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddPurchaseOrder(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<PurchaseOrder>(ReadBody(ctx.Request)); ApplyPurchaseOrder(item);
            var saved = PersistPurchaseOrderAdd(item, user);
            Audit(user, "新增采购单", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdatePurchaseOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<PurchaseOrder>(ReadBody(ctx.Request)); ApplyPurchaseOrder(input);
            var saved = PersistPurchaseOrderUpdate(id, input, user);
            Audit(user, "修改采购单", saved.Code); WriteJson(ctx, saved);
        }

        static void DeletePurchaseOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var orders = ReadJsonListCore<PurchaseOrder>(PurchaseOrdersFile);
                var item = orders.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购单不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("purchaseOrder", id, user, ctx, auditCode);
            PersistPurchaseOrderDelete(id, user);
            Audit(user, "删除采购单", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<PurchaseInbound> LoadPurchaseInbounds() { return LoadJsonList<PurchaseInbound>(PurchaseInboundsFile); }
        static void SavePurchaseInbounds(List<PurchaseInbound> items) { SaveJsonList(PurchaseInboundsFile, "purchase_inbounds", items); }

        static void ApplyPurchaseInbound(PurchaseInbound item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolvePurchaseOrderLink(item);
            item.SupplierName = (item.SupplierName ?? "").Trim();
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
            item.MaterialId = mid; item.MaterialCode = mcode; item.MaterialName = mname;
            item.PurchaseOrderId = (item.PurchaseOrderId ?? "").Trim();
            item.PurchaseNo = (item.PurchaseNo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("请选择物料");
            if (item.Quantity <= 0) BizFail("入库数量必须大于 0");
            ValidatePurchaseInboundRemainingQty(item);
            if (item.InboundPrice < 0) BizFail("入库单价不能为负数");
            item.Amount = CalcLineAmount(item.Quantity, item.InboundPrice);
            item.InboundDate = string.IsNullOrWhiteSpace(item.InboundDate) ? TodayText() : item.InboundDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddPurchaseInbound(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<PurchaseInbound>(ReadBody(ctx.Request)); ApplyPurchaseInbound(item);
            var saved = MutateJsonList<PurchaseInbound, PurchaseInbound>(PurchaseInboundsFile, "purchase_inbounds", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(PurchaseInboundSequenceFile, "PIN", list.Select(x => x.Code), "CGRK");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<PurchaseInbound>(item, true);
            });
            Audit(user, "新增采购入库", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdatePurchaseInbound(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<PurchaseInbound>(ReadBody(ctx.Request)); ApplyPurchaseInbound(input);
            ValidatePurchaseInboundRemainingQty(input, id);
            var saved = MutateJsonList<PurchaseInbound, PurchaseInbound>(PurchaseInboundsFile, "purchase_inbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购入库不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureConfirmedInventoryDocEditBlocked(item.Status, input.Status, "采购入库");
                if (IsConfirmedStatus(item.Status) && (item.Quantity != input.Quantity || !string.Equals(item.MaterialId ?? "", input.MaterialId ?? "", StringComparison.Ordinal)))
                    BizFail(ReferenceLockMessage, 409);
                item.PurchaseOrderId = input.PurchaseOrderId; item.PurchaseNo = input.PurchaseNo;
                item.SupplierName = input.SupplierName; item.MaterialId = input.MaterialId; item.MaterialCode = input.MaterialCode;
                item.MaterialName = input.MaterialName; item.Quantity = input.Quantity;
                item.InboundPrice = input.InboundPrice; item.Amount = input.Amount; item.InboundDate = input.InboundDate;
                item.Status = input.Status; item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<PurchaseInbound>(item, true);
            });
            Audit(user, "修改采购入库", saved.Code); WriteJson(ctx, saved);
        }

        static void DeletePurchaseInbound(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<PurchaseInbound>(PurchaseInboundsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购入库不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("purchaseInbound", id, user, ctx, auditCode);
            MutateJsonList<PurchaseInbound, object>(PurchaseInboundsFile, "purchase_inbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("采购入库不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除采购入库", auditCode); WriteJson(ctx, new { ok = true });
        }

        static void BatchDeleteSalesOrders(HttpListenerContext ctx, UserSession user) { BatchDeleteDocs<SalesOrder>(ctx, user, SalesOrdersFile, "sales_orders", "销售订单", "sales_order"); }
        static void BatchDeleteSalesOutbounds(HttpListenerContext ctx, UserSession user) { BatchDeleteDocs<SalesOutbound>(ctx, user, SalesOutboundsFile, "sales_outbounds", "销售出库", "sales_outbound"); }
        static void BatchDeletePurchaseOrders(HttpListenerContext ctx, UserSession user) { BatchDeleteDocs<PurchaseOrder>(ctx, user, PurchaseOrdersFile, "purchase_orders", "采购单", "purchase_order"); }
        static void BatchDeletePurchaseInbounds(HttpListenerContext ctx, UserSession user) { BatchDeleteDocs<PurchaseInbound>(ctx, user, PurchaseInboundsFile, "purchase_inbounds", "采购入库", "purchase_inbound"); }

        static void BatchDeleteDocs<T>(HttpListenerContext ctx, UserSession user, string file, string backupPrefix, string label, string auditKey) where T : class
        {
            var req = Json.Deserialize<BatchDeleteRequest>(ReadBody(ctx.Request));
            var ids = (req == null ? null : req.Ids) ?? new string[0];
            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            if (ids.Length == 0) { WriteJson(ctx, new { error = "请先选择要删除的数据" }, 400); return; }
            EnforceBatchDeleteImpact(AuditKeyToImpactModule(auditKey), ids, user, ctx);
            var deleted = MutateJsonList<T, int>(file, backupPrefix, list =>
            {
                var removed = list.Where(x => ids.Contains((string)x.GetType().GetProperty("Id").GetValue(x, null))).ToList();
                if (removed.Count == 0) throw new BusinessException("未找到可删除的" + label, 404);
                foreach (var item in removed) list.Remove(item);
                return new JsonMutationResult<int>(removed.Count, true);
            });
            Audit(user, "批量删除" + label, "共" + deleted + "条");
            WriteJson(ctx, new { ok = true, deleted = deleted });
        }

        static void BatchAddSalesOrders(HttpListenerContext ctx, UserSession user)
        {
            var req = DeserializeBatchSalesOrderRequest(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条销售订单" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<SalesOrder, object>(SalesOrdersFile, "sales_orders", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var pending = new List<SalesOrder>();
                foreach (var input in items)
                {
                    rowNo++;
                    try { ApplySalesOrder(input); var item = input; item.Id = Guid.NewGuid().ToString("N"); item.Code = string.IsNullOrWhiteSpace(input.Code) ? NextCode(SalesOrderSequenceFile, "SO", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "XSDD") : input.Code.Trim(); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; pending.Insert(0, item); imported++; }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加销售订单", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static void BatchAddSalesOutbounds(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchSalesOutboundRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条销售出库" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<SalesOutbound, object>(SalesOutboundsFile, "sales_outbounds", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var pending = new List<SalesOutbound>();
                foreach (var input in items)
                {
                    rowNo++;
                    try { ApplySalesOutbound(input); ValidateSalesOutboundRemainingQty(input); ValidateStockForConfirmedOutbound(input); var item = input; item.Id = Guid.NewGuid().ToString("N"); item.Code = string.IsNullOrWhiteSpace(input.Code) ? NextCode(SalesOutboundSequenceFile, "SOUT", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "XSCK") : input.Code.Trim(); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; pending.Insert(0, item); imported++; }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加销售出库", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static void BatchAddPurchaseOrders(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchPurchaseOrderRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条采购单" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<PurchaseOrder, object>(PurchaseOrdersFile, "purchase_orders", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var pending = new List<PurchaseOrder>();
                foreach (var input in items)
                {
                    rowNo++;
                    try { ApplyPurchaseOrder(input); var item = input; item.Id = Guid.NewGuid().ToString("N"); item.Code = string.IsNullOrWhiteSpace(input.Code) ? NextCode(PurchaseOrderSequenceFile, "PO", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "CGDD") : input.Code.Trim(); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; pending.Insert(0, item); imported++; }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加采购单", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static void BatchAddPurchaseInbounds(HttpListenerContext ctx, UserSession user)
        {
            var req = Json.Deserialize<BatchPurchaseInboundRequest>(ReadBody(ctx.Request));
            var items = req == null ? null : req.Items;
            if (items == null || items.Count == 0) { WriteJson(ctx, new { error = "请至少填写一条采购入库" }, 400); return; }
            int batchImported = 0, batchSkipped = 0;
            string[] batchErrors = new string[0];
            MutateJsonList<PurchaseInbound, object>(PurchaseInboundsFile, "purchase_inbounds", list =>
            {
                int imported = 0, skipped = 0, rowNo = 0;
                var errors = new List<string>();
                var pending = new List<PurchaseInbound>();
                foreach (var input in items)
                {
                    rowNo++;
                    try { ApplyPurchaseInbound(input); ValidatePurchaseInboundRemainingQty(input); var item = input; item.Id = Guid.NewGuid().ToString("N"); item.Code = string.IsNullOrWhiteSpace(input.Code) ? NextCode(PurchaseInboundSequenceFile, "PIN", list.Select(x => x.Code).Concat(pending.Select(x => x.Code)), "CGRK") : input.Code.Trim(); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; pending.Insert(0, item); imported++; }
                    catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
                }
                if (imported == 0) throw new BusinessException("没有可保存的数据", 409);
                foreach (var item in pending) list.Insert(0, item);
                batchImported = imported; batchSkipped = skipped; batchErrors = errors.ToArray();
                return new JsonMutationResult<object>(null, true);
            });
            Audit(user, "批量添加采购入库", "成功" + batchImported + "条，跳过" + batchSkipped + "条");
            WriteJson(ctx, new { imported = batchImported, skipped = batchSkipped, errors = batchErrors.Take(20).ToArray() });
        }

        static void ImportSalesOrders(HttpListenerContext ctx, UserSession user)
        {
            var rows = ReadImportRows(ctx); int imported = 0, skipped = 0; var errors = new List<string>(); int rowNo = 1;
            MutateJsonList<SalesOrder, object>(SalesOrdersFile, "sales_orders", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    var item = new SalesOrder { CustomerName = Cell(row, "客户名称"), CustomerCode = Cell(row, "客户编号"), MaterialName = Cell(row, "物料名称", "产品名称", "产品/物料名称"), Quantity = Money(Cell(row, "数量")), TaxExcludedSalePrice = Money(Cell(row, "不含税销售单价", "销售单价", "单价")), TaxIncludedSalePrice = Money(Cell(row, "含税销售单价")), OrderDate = Cell(row, "订单日期", "销售日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = Cell(row, "订单编号", "销售单号") };
                    if (Placeholder(item.CustomerName) && Placeholder(item.CustomerCode)) { skipped++; errors.Add("第" + rowNo + "行：请填写客户名称"); continue; }
                    if (Placeholder(item.CustomerName) && Placeholder(item.MaterialName)) { skipped++; continue; }
                    ApplySalesOrder(item); item.Id = Guid.NewGuid().ToString("N"); if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(SalesOrderSequenceFile, "SO", list.Select(x => x.Code), "XSDD"); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; list.Insert(0, item); imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user, "导入销售订单", "成功" + imported + "条，跳过" + skipped + "条"); WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(8).ToArray() });
        }

        static void ImportSalesOutbounds(HttpListenerContext ctx, UserSession user)
        {
            var rows = ReadImportRows(ctx); int imported = 0, skipped = 0; var errors = new List<string>(); int rowNo = 1;
            MutateJsonList<SalesOutbound, object>(SalesOutboundsFile, "sales_outbounds", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    var item = new SalesOutbound { SalesOrderNo = Cell(row, "销售订单号", "关联销售单号"), CustomerName = Cell(row, "客户名称"), MaterialName = Cell(row, "物料名称", "产品名称"), Quantity = Money(Cell(row, "出库数量", "数量")), CostPrice = Money(Cell(row, "成本单价", "单价")), OutboundDate = Cell(row, "出库日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = Cell(row, "出库编号", "出库单号") };
                    if (Placeholder(item.MaterialName)) { skipped++; continue; }
                    ApplySalesOutbound(item); item.Id = Guid.NewGuid().ToString("N"); if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(SalesOutboundSequenceFile, "SOUT", list.Select(x => x.Code), "XSCK"); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; list.Insert(0, item); imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user, "导入销售出库", "成功" + imported + "条，跳过" + skipped + "条"); WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(8).ToArray() });
        }

        static void ImportPurchaseOrders(HttpListenerContext ctx, UserSession user)
        {
            var rows = ReadImportRows(ctx); int imported = 0, skipped = 0; var errors = new List<string>(); int rowNo = 1;
            MutateJsonList<PurchaseOrder, object>(PurchaseOrdersFile, "purchase_orders", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    var item = new PurchaseOrder { SupplierName = Cell(row, "供应商名称"), MaterialName = Cell(row, "物料名称"), Quantity = Money(Cell(row, "数量")), UnitPrice = Money(Cell(row, "采购单价", "单价")), OrderDate = Cell(row, "订单日期", "采购日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = Cell(row, "采购编号", "采购单号") };
                    if (Placeholder(item.SupplierName) && Placeholder(item.MaterialName)) { skipped++; continue; }
                    ApplyPurchaseOrder(item); item.Id = Guid.NewGuid().ToString("N"); if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(PurchaseOrderSequenceFile, "PO", list.Select(x => x.Code), "CGDD"); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; list.Insert(0, item); imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user, "导入采购单", "成功" + imported + "条，跳过" + skipped + "条"); WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(8).ToArray() });
        }

        static void ImportPurchaseInbounds(HttpListenerContext ctx, UserSession user)
        {
            var rows = ReadImportRows(ctx); int imported = 0, skipped = 0; var errors = new List<string>(); int rowNo = 1;
            MutateJsonList<PurchaseInbound, object>(PurchaseInboundsFile, "purchase_inbounds", list =>
            {
            foreach (var row in rows)
            {
                rowNo++;
                try
                {
                    var item = new PurchaseInbound { PurchaseNo = Cell(row, "采购单号", "关联采购单号"), SupplierName = Cell(row, "供应商名称"), MaterialName = Cell(row, "物料名称"), Quantity = Money(Cell(row, "入库数量", "数量")), InboundPrice = Money(Cell(row, "入库单价", "单价")), InboundDate = Cell(row, "入库日期"), Status = Cell(row, "状态"), Note = Cell(row, "备注"), Code = Cell(row, "入库编号", "入库单号") };
                    if (Placeholder(item.MaterialName)) { skipped++; continue; }
                    ApplyPurchaseInbound(item); item.Id = Guid.NewGuid().ToString("N"); if (Placeholder(item.Code) || list.Any(x => x.Code == item.Code)) item.Code = NextCode(PurchaseInboundSequenceFile, "PIN", list.Select(x => x.Code), "CGRK"); item.UpdatedAt = NowTimeString(); item.UpdatedBy = user.DisplayName; list.Insert(0, item); imported++;
                }
                catch (Exception ex) { skipped++; errors.Add("第" + rowNo + "行：" + ex.Message); }
            }
            return new JsonMutationResult<object>(null, imported > 0);
            });
            Audit(user, "导入采购入库", "成功" + imported + "条，跳过" + skipped + "条"); WriteJson(ctx, new { imported = imported, skipped = skipped, errors = errors.Take(8).ToArray() });
        }

        static void ExportSalesOrdersCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("订单编号,订单日期,客户编号,客户名称,物料编号,物料名称,数量,不含税销售单价,含税销售单价,不含税销售金额,含税销售金额,状态,备注,最后更新,操作人");
            foreach (var x in LoadSalesOrders()) sb.AppendLine(string.Join(",", new[] { x.Code, x.OrderDate, x.CustomerCode, x.CustomerName, x.MaterialCode, x.MaterialName, x.Quantity.ToString("0.##"), x.TaxExcludedSalePrice.ToString("0.00"), x.TaxIncludedSalePrice.ToString("0.00"), x.TaxExcludedSaleAmount.ToString("0.00"), x.TaxIncludedSaleAmount.ToString("0.00"), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy }.Select(Csv)));
            WriteCsvAttachment(ctx, sb, BuildExportFileName("销售订单"));
        }

        static void ExportSalesOutboundsCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("出库编号,出库日期,销售订单号,客户名称,物料编号,物料名称,出库数量,成本单价,成本金额,状态,备注,最后更新,操作人");
            foreach (var x in LoadSalesOutbounds()) sb.AppendLine(string.Join(",", new[] { x.Code, x.OutboundDate, x.SalesOrderNo, x.CustomerName, x.MaterialCode, x.MaterialName, x.Quantity.ToString("0.##"), x.CostPrice.ToString("0.00"), x.CostAmount.ToString("0.00"), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy }.Select(Csv)));
            WriteCsvAttachment(ctx, sb, BuildExportFileName("销售出库"));
        }

        static void ExportPurchaseOrdersCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("采购编号,订单日期,供应商名称,物料编号,物料名称,数量,采购单价,采购金额,状态,备注,最后更新,操作人");
            foreach (var x in LoadPurchaseOrders()) sb.AppendLine(string.Join(",", new[] { x.Code, x.OrderDate, x.SupplierName, x.MaterialCode, x.MaterialName, x.Quantity.ToString("0.##"), x.UnitPrice.ToString("0.00"), x.Amount.ToString("0.00"), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy }.Select(Csv)));
            WriteCsvAttachment(ctx, sb, BuildExportFileName("采购单"));
        }

        static void ExportPurchaseInboundsCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder(); sb.AppendLine("入库编号,入库日期,采购单号,供应商名称,物料编号,物料名称,入库数量,入库单价,入库金额,状态,备注,最后更新,操作人");
            foreach (var x in LoadPurchaseInbounds()) sb.AppendLine(string.Join(",", new[] { x.Code, x.InboundDate, x.PurchaseNo, x.SupplierName, x.MaterialCode, x.MaterialName, x.Quantity.ToString("0.##"), x.InboundPrice.ToString("0.00"), x.Amount.ToString("0.00"), x.Status, x.Note, x.UpdatedAt, x.UpdatedBy }.Select(Csv)));
            WriteCsvAttachment(ctx, sb, BuildExportFileName("采购入库"));
        }

        static void WriteCsvAttachment(HttpListenerContext ctx, StringBuilder sb, string fileName)
        {
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            ctx.Response.ContentType = "text/csv; charset=utf-8"; ctx.Response.AddHeader("Content-Disposition", BuildExportContentDisposition(fileName)); ctx.Response.ContentLength64 = bytes.Length; ctx.Response.OutputStream.Write(bytes, 0, bytes.Length); ctx.Response.Close();
        }

        static List<ProductionPick> LoadProductionPicks() { return LoadJsonList<ProductionPick>(ProductionPicksFile); }
        static void SaveProductionPicks(List<ProductionPick> items) { SaveJsonList(ProductionPicksFile, "production_picks", items); }

        static void ApplyProductionPick(ProductionPick item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveBomLinkForPick(item);
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(item.MaterialId, item.MaterialName, out mid, out mcode, out mname, out mspec, out munit);
            item.MaterialId = mid; item.MaterialCode = mcode; item.MaterialName = mname;
            item.BomId = (item.BomId ?? "").Trim();
            item.BomName = (item.BomName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.MaterialName)) BizFail("请选择物料");
            if (item.Quantity <= 0) BizFail("领用数量必须大于 0");
            AutoResolveProductionPickCost(item);
            if (item.CostPrice < 0) BizFail("成本单价不能为负数");
            item.CostAmount = CalcLineAmount(item.Quantity, item.CostPrice);
            item.PickDate = string.IsNullOrWhiteSpace(item.PickDate) ? TodayText() : item.PickDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddProductionPick(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ProductionPick>(ReadBody(ctx.Request)); ApplyProductionPick(item);
            ValidateStockForConfirmedPick(item);
            var saved = MutateJsonList<ProductionPick, ProductionPick>(ProductionPicksFile, "production_picks", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(ProductionPickSequenceFile, "PL", list.Select(x => x.Code), "SCLL");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<ProductionPick>(item, true);
            });
            Audit(user, "新增生产领用", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdateProductionPick(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ProductionPick>(ReadBody(ctx.Request)); ApplyProductionPick(input);
            ValidateStockForConfirmedPick(input, id);
            var saved = MutateJsonList<ProductionPick, ProductionPick>(ProductionPicksFile, "production_picks", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产领用不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (IsConfirmedStatus(item.Status) && IsConfirmedStatus(input.Status))
                    BizFail("该生产领用已确认，不能重复确认。", 409);
                EnsureConfirmedInventoryDocEditBlocked(item.Status, input.Status, "生产领用");
                if (IsConfirmedStatus(item.Status) && (item.Quantity != input.Quantity || !string.Equals(item.MaterialId ?? "", input.MaterialId ?? "", StringComparison.Ordinal)))
                    BizFail(ReferenceLockMessage, 409);
                item.BomId = input.BomId; item.BomName = input.BomName;
                item.MaterialId = input.MaterialId; item.MaterialCode = input.MaterialCode; item.MaterialName = input.MaterialName;
                item.Quantity = input.Quantity; item.CostPrice = input.CostPrice;
                item.CostAmount = input.CostAmount; item.PickDate = input.PickDate; item.Status = input.Status;
                item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<ProductionPick>(item, true);
            });
            Audit(user, "修改生产领用", saved.Code); WriteJson(ctx, saved);
        }

        static void DeleteProductionPick(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<ProductionPick>(ProductionPicksFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产领用不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("productionPick", id, user, ctx, auditCode);
            MutateJsonList<ProductionPick, object>(ProductionPicksFile, "production_picks", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产领用不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除生产领用", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<ProductionWorkOrder> LoadProductionWorkOrders() { return LoadJsonList<ProductionWorkOrder>(ProductionWorkOrdersFile); }

        static string NextProductionWorkOrderNo(IEnumerable<ProductionWorkOrder> list)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string prefix = "WO" + datePart;
            int max = 0;
            foreach (var x in list ?? Enumerable.Empty<ProductionWorkOrder>())
            {
                var code = x.WorkOrderNo ?? "";
                if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                int seq;
                if (code.Length > prefix.Length && int.TryParse(code.Substring(prefix.Length), out seq))
                    max = Math.Max(max, seq);
            }
            return prefix + (max + 1).ToString("D3");
        }

        static string NormalizeProductionWorkOrderStatus(string status)
        {
            status = (status ?? "").Trim();
            if (status == "草稿" || status == "待生产" || status == "生产中" || status == "部分完工" || status == "已完成" || status == "已取消") return status;
            return "草稿";
        }

        static bool CanEditProductionWorkOrder(string status)
        {
            status = NormalizeProductionWorkOrderStatus(status);
            return status == "草稿" || status == "待生产" || status == "生产中" || status == "部分完工";
        }

        static bool CanDeleteProductionWorkOrder(string status)
        {
            status = NormalizeProductionWorkOrderStatus(status);
            return status == "草稿" || status == "已取消";
        }

        static void SyncProductionWorkOrderQuantities(ProductionWorkOrder item)
        {
            if (item == null) return;
            item.ProducedQuantity = Math.Max(0, item.ProducedQuantity);
            if (item.ProducedQuantity > item.Quantity) BizFail("已完工数量不能大于计划数量");
            item.UnproducedQuantity = item.Quantity - item.ProducedQuantity;
        }

        static void SyncProductionWorkOrderFinishStatus(ProductionWorkOrder item)
        {
            if (item == null) return;
            if (item.Status == "已取消" || item.Status == "草稿" || item.Status == "待生产") return;
            if (item.ProducedQuantity >= item.Quantity)
            {
                item.Status = "已完成";
                if (string.IsNullOrWhiteSpace(item.ActualFinishDate)) item.ActualFinishDate = TodayText();
            }
            else if (item.ProducedQuantity > 0)
                item.Status = "部分完工";
        }

        static void ResolveProductionWorkOrderSalesOrder(ProductionWorkOrder item)
        {
            item.SalesOrderId = (item.SalesOrderId ?? "").Trim();
            item.SalesOrderNo = (item.SalesOrderNo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.SalesOrderId) && string.IsNullOrWhiteSpace(item.SalesOrderNo)) return;
            var orders = LoadSalesOrders();
            SalesOrder order = null;
            if (!string.IsNullOrWhiteSpace(item.SalesOrderId))
                order = orders.FirstOrDefault(x => x.Id == item.SalesOrderId);
            if (order == null && !string.IsNullOrWhiteSpace(item.SalesOrderNo))
                order = orders.FirstOrDefault(x => string.Equals(x.Code, item.SalesOrderNo, StringComparison.OrdinalIgnoreCase));
            if (order == null) BizFail("来源销售订单不存在");
            item.SalesOrderId = order.Id;
            item.SalesOrderNo = order.Code ?? "";
            item.CustomerId = order.CustomerId ?? "";
            item.CustomerName = order.CustomerName ?? "";
            item.ProductName = order.MaterialName ?? "";
            if (order.Quantity > 0) item.Quantity = order.Quantity;
            item.Unit = "台";
            if (!string.IsNullOrWhiteSpace(order.BomId)) item.BomId = order.BomId;
            if (!string.IsNullOrWhiteSpace(order.ModelCostId)) item.ModelCostId = order.ModelCostId;
            item.SourceType = "销售订单";
        }

        static void ResolveProductionWorkOrderCustomer(ProductionWorkOrder item, bool fromSalesOrder)
        {
            item.CustomerId = (item.CustomerId ?? "").Trim();
            item.CustomerName = (item.CustomerName ?? "").Trim();
            if (fromSalesOrder) return;
            if (!string.IsNullOrWhiteSpace(item.CustomerId))
            {
                var byId = LoadCustomers().FirstOrDefault(x => x.Id == item.CustomerId);
                if (byId != null)
                {
                    item.CustomerId = byId.Id;
                    item.CustomerName = byId.Company ?? "";
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(item.CustomerName))
            {
                var byName = LoadCustomers().FirstOrDefault(x => string.Equals(x.Company, item.CustomerName, StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                {
                    item.CustomerId = byName.Id;
                    item.CustomerName = byName.Company ?? "";
                    return;
                }
            }
            if (string.Equals(item.SourceType ?? "", "手工", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(item.SalesOrderId))
                BizFail("请选择客户");
        }

        static void ResolveProductionWorkOrderBom(ProductionWorkOrder item)
        {
            item.BomId = (item.BomId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.BomId)) return;
            var bom = LoadBom().FirstOrDefault(x => x.Id == item.BomId);
            if (bom == null) BizFail("来源 BOM 不存在");
            item.BomName = !string.IsNullOrWhiteSpace(bom.ModelName) ? bom.ModelName : (bom.ProductName ?? "");
            item.ProductName = !string.IsNullOrWhiteSpace(bom.ProductName) ? bom.ProductName : (bom.ModelName ?? "");
            item.Spec = bom.ModelCode ?? "";
        }

        static void ResolveProductionWorkOrderModelCost(ProductionWorkOrder item)
        {
            item.ModelCostId = (item.ModelCostId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.ModelCostId)) return;
            var mc = LoadModelCosts().FirstOrDefault(x => x.Id == item.ModelCostId);
            if (mc == null) BizFail("机型成本不存在");
            item.UnitCost = mc.TotalCost > 0 ? mc.TotalCost : mc.MaterialCost;
            if (string.IsNullOrWhiteSpace(item.BomId) && !string.IsNullOrWhiteSpace(mc.BomId))
                item.BomId = mc.BomId;
            if (string.IsNullOrWhiteSpace(item.BomId))
            {
                item.ProductName = !string.IsNullOrWhiteSpace(mc.ProductName) ? mc.ProductName : (mc.ModelName ?? "");
                item.Spec = mc.ModelCode ?? "";
            }
        }

        static void ApplyProductionWorkOrder(ProductionWorkOrder item, bool preserveProgressFields)
        {
            if (item == null) BizFail("数据不能为空");
            item.SourceType = string.IsNullOrWhiteSpace(item.SourceType) ? "手工" : item.SourceType.Trim();
            bool fromSalesOrder = string.Equals(item.SourceType, "销售订单", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(item.SalesOrderId) || !string.IsNullOrWhiteSpace(item.SalesOrderNo);
            if (string.Equals(item.SourceType, "销售订单", StringComparison.OrdinalIgnoreCase))
                ResolveProductionWorkOrderSalesOrder(item);
            else if (!string.IsNullOrWhiteSpace(item.SalesOrderId) || !string.IsNullOrWhiteSpace(item.SalesOrderNo))
                ResolveProductionWorkOrderSalesOrder(item);
            ResolveProductionWorkOrderCustomer(item, fromSalesOrder && !string.IsNullOrWhiteSpace(item.SalesOrderId));
            ResolveProductionWorkOrderModelCost(item);
            ResolveProductionWorkOrderBom(item);
            if (string.IsNullOrWhiteSpace(item.ProductName)) BizFail("请填写产品/机型名称");
            if (item.Quantity <= 0) BizFail("计划生产数量必须大于 0");
            if (string.IsNullOrWhiteSpace(item.Unit)) item.Unit = "台";
            item.WorkOrderDate = string.IsNullOrWhiteSpace(item.WorkOrderDate) ? TodayText() : item.WorkOrderDate.Trim();
            item.PlannedStartDate = (item.PlannedStartDate ?? "").Trim();
            item.PlannedFinishDate = (item.PlannedFinishDate ?? "").Trim();
            item.Remark = (item.Remark ?? "").Trim();
            if (!preserveProgressFields)
            {
                item.ProducedQuantity = 0;
                item.UnproducedQuantity = item.Quantity;
                item.PickedMaterialAmount = 0;
                item.FinishedInboundAmount = 0;
            }
            SyncProductionWorkOrderQuantities(item);
            item.Status = NormalizeProductionWorkOrderStatus(item.Status);
            if (string.IsNullOrWhiteSpace(item.Status)) item.Status = "草稿";
        }

        static void CopyProductionWorkOrderEditableFields(ProductionWorkOrder target, ProductionWorkOrder input, bool coreEditable)
        {
            target.WorkOrderDate = input.WorkOrderDate;
            target.SourceType = input.SourceType;
            target.SalesOrderId = input.SalesOrderId;
            target.SalesOrderNo = input.SalesOrderNo;
            target.CustomerId = input.CustomerId;
            target.CustomerName = input.CustomerName;
            if (coreEditable)
            {
                target.ProductName = input.ProductName;
                target.Spec = input.Spec;
                target.Quantity = input.Quantity;
                target.Unit = input.Unit;
                target.BomId = input.BomId;
                target.BomName = input.BomName;
                target.ModelCostId = input.ModelCostId;
                target.UnitCost = input.UnitCost;
            }
            target.PlannedStartDate = input.PlannedStartDate;
            target.PlannedFinishDate = input.PlannedFinishDate;
            target.Remark = input.Remark;
            if (coreEditable && (target.Status == "草稿" || target.Status == "待生产"))
                target.Status = NormalizeProductionWorkOrderStatus(input.Status);
        }

        static void GetProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var item = LoadProductionWorkOrders().FirstOrDefault(x => x.Id == id);
            if (item == null) throw new BusinessException("生产工单不存在", 404);
            WriteJson(ctx, item);
        }

        static void AddProductionWorkOrder(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<ProductionWorkOrder>(ReadBody(ctx.Request));
            ApplyProductionWorkOrder(item, false);
            string now = BizUpdatedAtNow();
            var saved = MutateJsonList<ProductionWorkOrder, ProductionWorkOrder>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.WorkOrderNo = NextProductionWorkOrderNo(list);
                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.CreatedBy = user.DisplayName;
                item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<ProductionWorkOrder>(item, true);
            });
            Audit(user, "新增生产工单", saved.WorkOrderNo);
            WriteJson(ctx, saved, 201);
        }

        static void UpdateProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<ProductionWorkOrder>(ReadBody(ctx.Request));
            var saved = MutateJsonList<ProductionWorkOrder, ProductionWorkOrder>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                if (!CanEditProductionWorkOrder(item.Status)) BizFail("当前状态不允许编辑", 409);
                bool coreEditable = item.Status == "草稿" || item.Status == "待生产";
                var produced = item.ProducedQuantity;
                var picked = item.PickedMaterialAmount;
                var finishedAmt = item.FinishedInboundAmount;
                var actualStart = item.ActualStartDate;
                var actualFinish = item.ActualFinishDate;
                var status = item.Status;
                ApplyProductionWorkOrder(input, true);
                CopyProductionWorkOrderEditableFields(item, input, coreEditable);
                item.ProducedQuantity = produced;
                item.PickedMaterialAmount = picked;
                item.FinishedInboundAmount = finishedAmt;
                item.ActualStartDate = actualStart;
                item.ActualFinishDate = actualFinish;
                if (!coreEditable) item.Status = status;
                SyncProductionWorkOrderQuantities(item);
                SyncProductionWorkOrderFinishStatus(item);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<ProductionWorkOrder>(item, true);
            });
            Audit(user, "修改生产工单", saved.WorkOrderNo);
            WriteJson(ctx, saved);
        }

        static void DeleteProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<ProductionWorkOrder>(ProductionWorkOrdersFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                if (!CanDeleteProductionWorkOrder(item.Status)) throw new BusinessException("仅草稿或已取消的工单可以删除", 409);
                auditCode = item.WorkOrderNo;
            });
            MutateJsonList<ProductionWorkOrder, object>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                if (!CanDeleteProductionWorkOrder(item.Status)) throw new BusinessException("仅草稿或已取消的工单可以删除", 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除生产工单", auditCode);
            WriteJson(ctx, new { ok = true });
        }

        static void StartProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var saved = MutateJsonList<ProductionWorkOrder, ProductionWorkOrder>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                var status = NormalizeProductionWorkOrderStatus(item.Status);
                if (status != "草稿" && status != "待生产") BizFail("仅草稿或待生产工单可以开始生产", 409);
                item.Status = "生产中";
                if (string.IsNullOrWhiteSpace(item.ActualStartDate)) item.ActualStartDate = TodayText();
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<ProductionWorkOrder>(item, true);
            });
            Audit(user, "开始生产", saved.WorkOrderNo);
            WriteJson(ctx, saved);
        }

        static void FinishProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var req = Json.Deserialize<ProductionWorkOrderFinishRequest>(ReadBody(ctx.Request));
            if (req == null || req.FinishQuantity <= 0) BizFail("本次完工数量必须大于 0");
            var saved = MutateJsonList<ProductionWorkOrder, ProductionWorkOrder>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, req.UpdatedAt);
                var status = NormalizeProductionWorkOrderStatus(item.Status);
                if (status != "生产中" && status != "部分完工") BizFail("仅生产中或部分完工工单可以登记完工", 409);
                var remaining = item.Quantity - item.ProducedQuantity;
                if (req.FinishQuantity > remaining + 0.0001m) BizFail("本次完工数量不能超过未完工数量 " + remaining.ToString("0.##"), 409);
                item.ProducedQuantity += req.FinishQuantity;
                SyncProductionWorkOrderQuantities(item);
                SyncProductionWorkOrderFinishStatus(item);
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<ProductionWorkOrder>(item, true);
            });
            Audit(user, "工单完工登记", saved.WorkOrderNo + " +" + req.FinishQuantity.ToString("0.##"));
            WriteJson(ctx, saved);
        }

        static void CancelProductionWorkOrder(HttpListenerContext ctx, UserSession user, string id)
        {
            var saved = MutateJsonList<ProductionWorkOrder, ProductionWorkOrder>(ProductionWorkOrdersFile, "production_work_orders", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("生产工单不存在", 404);
                if (NormalizeProductionWorkOrderStatus(item.Status) == "已完成") BizFail("已完成的工单不能取消", 409);
                item.Status = "已取消";
                item.UpdatedAt = BizUpdatedAtNow();
                item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<ProductionWorkOrder>(item, true);
            });
            Audit(user, "取消生产工单", saved.WorkOrderNo);
            WriteJson(ctx, saved);
        }

        static List<FinishedInbound> LoadFinishedInbounds() { return LoadJsonList<FinishedInbound>(FinishedInboundsFile); }
        static void SaveFinishedInbounds(List<FinishedInbound> items) { SaveJsonList(FinishedInboundsFile, "finished_inbounds", items); }

        static void ApplyFinishedInbound(FinishedInbound item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveBomAndModelCostLink(item);
            if (string.IsNullOrWhiteSpace(item.BomId) && string.IsNullOrWhiteSpace(item.ModelCostId))
                BizFail("成品入库必须关联 BOM 或机型成本");
            item.ProductName = (item.ProductName ?? "").Trim();
            item.BomId = (item.BomId ?? "").Trim();
            item.BomCode = (item.BomCode ?? "").Trim();
            item.ModelCostId = (item.ModelCostId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.ProductName)) BizFail("请填写产品名称");
            if (item.Quantity <= 0) BizFail("入库数量必须大于 0");
            AutoResolveFinishedInboundUnitCost(item);
            if (item.UnitCost < 0) BizFail("单台成本不能为负数");
            item.Amount = CalcLineAmount(item.Quantity, item.UnitCost);
            item.InboundDate = string.IsNullOrWhiteSpace(item.InboundDate) ? TodayText() : item.InboundDate.Trim();
            item.Status = NormalizeDocStatus(item.Status);
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddFinishedInbound(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<FinishedInbound>(ReadBody(ctx.Request)); ApplyFinishedInbound(item);
            var saved = MutateJsonList<FinishedInbound, FinishedInbound>(FinishedInboundsFile, "finished_inbounds", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(FinishedInboundSequenceFile, "FGI", list.Select(x => x.Code), "CPRK");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<FinishedInbound>(item, true);
            });
            Audit(user, "新增成品入库", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdateFinishedInbound(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<FinishedInbound>(ReadBody(ctx.Request)); ApplyFinishedInbound(input);
            var saved = MutateJsonList<FinishedInbound, FinishedInbound>(FinishedInboundsFile, "finished_inbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("成品入库不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                EnsureConfirmedInventoryDocEditBlocked(item.Status, input.Status, "成品入库");
                if (IsConfirmedStatus(item.Status) && item.Quantity != input.Quantity)
                    BizFail(ReferenceLockMessage, 409);
                item.BomId = input.BomId; item.BomCode = input.BomCode; item.ModelCostId = input.ModelCostId;
                item.ProductName = input.ProductName; item.Quantity = input.Quantity; item.UnitCost = input.UnitCost;
                item.Amount = input.Amount; item.InboundDate = input.InboundDate; item.Status = input.Status;
                item.Note = input.Note; item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<FinishedInbound>(item, true);
            });
            Audit(user, "修改成品入库", saved.Code); WriteJson(ctx, saved);
        }

        static void DeleteFinishedInbound(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<FinishedInbound>(FinishedInboundsFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("成品入库不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("finishedInbound", id, user, ctx, auditCode);
            MutateJsonList<FinishedInbound, object>(FinishedInboundsFile, "finished_inbounds", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("成品入库不存在", 404);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除成品入库", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<Receivable> LoadReceivables() { return LoadJsonList<Receivable>(ReceivablesFile); }
        static void SaveReceivables(List<Receivable> items) { SaveJsonList(ReceivablesFile, "receivables", items); }

        static void ApplyReceivable(Receivable item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolveReceivableSalesOrderLink(item);
            item.CustomerName = (item.CustomerName ?? "").Trim();
            item.SalesOrderId = (item.SalesOrderId ?? "").Trim();
            item.SalesOrderNo = (item.SalesOrderNo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.CustomerName)) BizFail("请填写客户名称");
            if (item.ReceivableAmount < 0) BizFail("应收金额不能为负数");
            if (item.ReceiptDetails == null) item.ReceiptDetails = new List<ReceiptDetail>();
            SyncReceivableAmountsFromDetails(item);
            ValidateReceivableTotals(item);
            item.DueDate = (item.DueDate ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddReceivable(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Receivable>(ReadBody(ctx.Request)); ApplyReceivable(item);
            var saved = MutateJsonList<Receivable, Receivable>(ReceivablesFile, "receivables", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(ReceivableSequenceFile, "AR", list.Select(x => x.Code), "YS");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<Receivable>(item, true);
            });
            Audit(user, "新增应收款", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdateReceivable(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Receivable>(ReadBody(ctx.Request)); ApplyReceivable(input);
            var saved = MutateJsonList<Receivable, Receivable>(ReceivablesFile, "receivables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应收款不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                item.CustomerName = input.CustomerName; item.SalesOrderId = input.SalesOrderId; item.SalesOrderNo = input.SalesOrderNo;
                item.ReceivableAmount = input.ReceivableAmount;
                if (item.ReceiptDetails == null || item.ReceiptDetails.Count == 0)
                    item.ReceivedAmount = input.ReceivedAmount;
                item.DueDate = input.DueDate; item.Note = input.Note;
                SyncReceivableAmountsFromDetails(item);
                ValidateReceivableTotals(item);
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<Receivable>(item, true);
            });
            Audit(user, "修改应收款", saved.Code); WriteJson(ctx, saved);
        }

        static void DeleteReceivable(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<Receivable>(ReceivablesFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应收款不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("receivable", id, user, ctx, auditCode);
            MutateJsonList<Receivable, object>(ReceivablesFile, "receivables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应收款不存在", 404);
                if (item.ReceiptDetails != null && item.ReceiptDetails.Count > 0)
                    BizFail("该应收款已有收款明细，请先删除收款明细后再删除应收款。", 409);
                if (item.ReceivedAmount > 0)
                    BizFail("该应收款已有收款记录，请先删除收款明细后再删除应收款。", 409);
                if (IsAutoSource(item.SourceType) || !string.IsNullOrWhiteSpace(item.SalesOrderId) || !string.IsNullOrWhiteSpace(item.SalesOrderNo))
                    BizFail("该应收款由销售订单自动生成或关联销售订单，不能删除。请先处理来源销售订单。", 409);
                if (!string.IsNullOrWhiteSpace(item.ServiceOrderId) || !string.IsNullOrWhiteSpace(item.ServiceOrderNo)
                    || string.Equals(item.SourceType ?? "", "售后维修", StringComparison.OrdinalIgnoreCase))
                    BizFail("该应收款关联售后维修工单，不能删除。请先处理来源维修工单。", 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除应收款", auditCode); WriteJson(ctx, new { ok = true });
        }

        static List<Payable> LoadPayables() { return LoadJsonList<Payable>(PayablesFile); }
        static void SavePayables(List<Payable> items) { SaveJsonList(PayablesFile, "payables", items); }

        static void ApplyPayable(Payable item)
        {
            if (item == null) BizFail("数据不能为空");
            ResolvePayablePurchaseOrderLink(item);
            item.SupplierName = (item.SupplierName ?? "").Trim();
            item.PurchaseOrderId = (item.PurchaseOrderId ?? "").Trim();
            item.PurchaseNo = (item.PurchaseNo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(item.SupplierName)) BizFail("请填写供应商名称");
            if (item.PayableAmount < 0) BizFail("应付金额不能为负数");
            if (item.PaymentDetails == null) item.PaymentDetails = new List<PaymentDetail>();
            SyncPayableAmountsFromDetails(item);
            ValidatePayableTotals(item);
            item.DueDate = (item.DueDate ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();
        }

        static void AddPayable(HttpListenerContext ctx, UserSession user)
        {
            var item = Json.Deserialize<Payable>(ReadBody(ctx.Request)); ApplyPayable(item);
            var saved = MutateJsonList<Payable, Payable>(PayablesFile, "payables", list =>
            {
                item.Id = Guid.NewGuid().ToString("N");
                item.Code = NextCode(PayableSequenceFile, "AP", list.Select(x => x.Code), "YF");
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                list.Insert(0, item);
                return new JsonMutationResult<Payable>(item, true);
            });
            Audit(user, "新增应付款", saved.Code); WriteJson(ctx, saved, 201);
        }

        static void UpdatePayable(HttpListenerContext ctx, UserSession user, string id)
        {
            var input = Json.Deserialize<Payable>(ReadBody(ctx.Request)); ApplyPayable(input);
            var saved = MutateJsonList<Payable, Payable>(PayablesFile, "payables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应付款不存在", 404);
                EnsureEditVersionMatch(item.UpdatedAt, input.UpdatedAt);
                item.SupplierName = input.SupplierName; item.PurchaseOrderId = input.PurchaseOrderId; item.PurchaseNo = input.PurchaseNo;
                item.PayableAmount = input.PayableAmount;
                if (item.PaymentDetails == null || item.PaymentDetails.Count == 0)
                    item.PaidAmount = input.PaidAmount;
                item.DueDate = input.DueDate; item.Note = input.Note;
                SyncPayableAmountsFromDetails(item);
                ValidatePayableTotals(item);
                item.UpdatedAt = BizUpdatedAtNow(); item.UpdatedBy = user.DisplayName;
                return new JsonMutationResult<Payable>(item, true);
            });
            Audit(user, "修改应付款", saved.Code); WriteJson(ctx, saved);
        }

        static void DeletePayable(HttpListenerContext ctx, UserSession user, string id)
        {
            string auditCode = null;
            RunUnderDataLock(() =>
            {
                var item = ReadJsonListCore<Payable>(PayablesFile).FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应付款不存在", 404);
                auditCode = item.Code;
            });
            EnforceDeleteImpact("payable", id, user, ctx, auditCode);
            MutateJsonList<Payable, object>(PayablesFile, "payables", list =>
            {
                var item = list.FirstOrDefault(x => x.Id == id);
                if (item == null) throw new BusinessException("应付款不存在", 404);
                if (item.PaymentDetails != null && item.PaymentDetails.Count > 0)
                    BizFail("该应付款已有付款明细，请先删除付款明细后再删除应付款。", 409);
                if (item.PaidAmount > 0)
                    BizFail("该应付款已有付款记录，请先删除付款明细后再删除应付款。", 409);
                if (IsAutoSource(item.SourceType) || !string.IsNullOrWhiteSpace(item.PurchaseOrderId) || !string.IsNullOrWhiteSpace(item.PurchaseNo))
                    BizFail("该应付款由采购单自动生成或关联采购单，不能删除。请先处理来源采购单。", 409);
                list.Remove(item);
                return new JsonMutationResult<object>(new { ok = true }, true);
            });
            Audit(user, "删除应付款", auditCode); WriteJson(ctx, new { ok = true });
        }

        class StockAgg
        {
            public string ItemType;
            public string ItemId;
            public string ItemCode;
            public string ItemName;
            public string Spec;
            public string Unit;
            public decimal Quantity;
            public decimal CostAmount;
        }

        static string StockKey(string itemType, string itemId, string fallbackName)
        {
            itemType = (itemType ?? "").Trim();
            itemId = (itemId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(itemId)) return itemType + "|" + itemId;
            return itemType + "|name:" + (fallbackName ?? "").Trim();
        }

        static void StockAdd(Dictionary<string, StockAgg> map, string itemType, string itemId, string itemCode, string itemName, string spec, string unit, decimal qty, decimal unitCost)
        {
            if (qty == 0) return;
            itemName = (itemName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemId)) return;
            string key = StockKey(itemType, itemId, itemName);
            StockAgg agg;
            if (!map.TryGetValue(key, out agg))
            {
                agg = new StockAgg
                {
                    ItemType = itemType,
                    ItemId = itemId ?? "",
                    ItemCode = itemCode ?? "",
                    ItemName = itemName,
                    Spec = spec ?? "",
                    Unit = unit ?? ""
                };
            }
            if (string.IsNullOrWhiteSpace(agg.ItemCode) && !string.IsNullOrWhiteSpace(itemCode)) agg.ItemCode = itemCode;
            if (string.IsNullOrWhiteSpace(agg.Spec) && !string.IsNullOrWhiteSpace(spec)) agg.Spec = spec;
            if (string.IsNullOrWhiteSpace(agg.Unit) && !string.IsNullOrWhiteSpace(unit)) agg.Unit = unit;
            agg.Quantity += qty;
            if (qty > 0 && unitCost >= 0) agg.CostAmount += CalcLineAmount(qty, unitCost);
            map[key] = agg;
        }

        static void StockAddMaterial(Dictionary<string, StockAgg> map, string materialId, string materialCode, string materialName, decimal qty, decimal unitCost)
        {
            string mid, mcode, mname, mspec, munit;
            ResolveMaterialFields(materialId, materialName, out mid, out mcode, out mname, out mspec, out munit);
            if (!string.IsNullOrWhiteSpace(materialCode)) mcode = materialCode;
            StockAdd(map, "物料", mid, mcode, mname, mspec, munit, qty, unitCost);
        }

        static Dictionary<string, StockAgg> BuildStockMap(StockMapOptions options = null)
        {
            options = options ?? new StockMapOptions();
            var map = new Dictionary<string, StockAgg>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in LoadPurchaseInbounds().Where(x => IsConfirmedStatus(x.Status)))
                StockAddMaterial(map, x.MaterialId, x.MaterialCode, x.MaterialName, x.Quantity, x.InboundPrice);
            foreach (var x in LoadFinishedInbounds().Where(x => IsConfirmedStatus(x.Status)))
            {
                string pid = !string.IsNullOrWhiteSpace(x.ModelCostId) ? x.ModelCostId : x.BomId;
                StockAdd(map, "成品", pid, x.BomCode, x.ProductName, "", "", x.Quantity, x.UnitCost);
            }
            foreach (var x in LoadSalesOutbounds().Where(x => IsConfirmedStatus(x.Status) && x.Id != options.ExcludeSalesOutboundId))
            {
                if (IsFinishedProductOutbound(x))
                {
                    string pid = GetFinishedProductStockId(x.ModelCostId, x.BomId);
                    StockAdd(map, "成品", pid, x.BomCode ?? "", x.MaterialName ?? "", "", "", -x.Quantity, x.CostPrice);
                }
                else
                    StockAddMaterial(map, x.MaterialId, x.MaterialCode, x.MaterialName, -x.Quantity, x.CostPrice);
            }
            foreach (var x in LoadProductionPicks().Where(x => IsConfirmedStatus(x.Status) && x.Id != options.ExcludeProductionPickId))
                StockAddMaterial(map, x.MaterialId, x.MaterialCode, x.MaterialName, -x.Quantity, x.CostPrice);
            return map;
        }

        static List<StockItem> BuildStockItems()
        {
            return BuildStockMap().Values.Select(agg =>
            {
                decimal qty = RoundMoney(agg.Quantity);
                decimal costPrice = qty == 0 ? 0 : RoundMoney(agg.CostAmount / Math.Abs(qty));
                if (qty < 0) costPrice = agg.CostAmount > 0 ? RoundMoney(agg.CostAmount / Math.Abs(qty)) : 0;
                string stockType = "成品设备";
                bool isFinished = string.Equals(agg.ItemType, "成品", StringComparison.OrdinalIgnoreCase);
                bool isService = false;
                decimal safetyStock = 0;
                bool? isInventory = true;
                string defaultWarehouse = "默认仓库";
                string costMethod = "机型成本快照";
                if (!isFinished)
                {
                    var mat = FindMaterialForStockItem(agg.ItemType, agg.ItemId, agg.ItemCode);
                    if (mat != null)
                    {
                        stockType = mat.StockType ?? "外购配件";
                        isFinished = mat.IsFinishedGood;
                        isService = mat.IsServicePart;
                        safetyStock = mat.SafetyStock;
                        isInventory = mat.IsInventoryItem ?? true;
                        defaultWarehouse = string.IsNullOrWhiteSpace(mat.DefaultWarehouse) ? "默认仓库" : mat.DefaultWarehouse;
                        costMethod = string.IsNullOrWhiteSpace(mat.CostMethod) ? "固定成本价" : mat.CostMethod;
                    }
                    else
                    {
                        stockType = "外购配件";
                        costMethod = "固定成本价";
                    }
                }
                return new StockItem
                {
                    ItemType = agg.ItemType,
                    ItemId = agg.ItemId,
                    ItemCode = agg.ItemCode,
                    ItemName = agg.ItemName,
                    Spec = agg.Spec,
                    Unit = agg.Unit,
                    WarehouseName = defaultWarehouse,
                    CurrentQuantity = qty,
                    CostPrice = costPrice,
                    StockAmount = RoundMoney(Math.Abs(qty) * costPrice),
                    StockType = stockType,
                    IsFinishedGood = isFinished,
                    IsServicePart = isService,
                    SafetyStock = safetyStock,
                    StockStatus = ComputeStockStatusLabel(qty, safetyStock),
                    IsInventoryItem = isInventory,
                    DefaultWarehouse = defaultWarehouse,
                    CostMethod = costMethod
                };
            }).OrderBy(x => x.ItemType).ThenBy(x => x.ItemName).ToList();
        }

        static StockSummary BuildStockSummary()
        {
            var items = BuildStockItems();
            return new StockSummary
            {
                ItemCount = items.Count,
                TotalQuantity = RoundMoney(items.Sum(x => x.CurrentQuantity)),
                Items = items.ToArray()
            };
        }

        static void ExportStocksCsv(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("物料编码,名称,规格,单位,库存类型,是否库存物料,仓库,默认仓库,当前库存,安全库存,库存状态,成本方式,成本单价,库存金额");
            foreach (var x in BuildStockItems())
            {
                bool inv = x.IsInventoryItem ?? true;
                sb.AppendLine(string.Join(",", new[]
                {
                    x.ItemCode, x.ItemName, x.Spec, x.Unit, x.StockType,
                    inv ? "是" : "否",
                    x.WarehouseName, x.DefaultWarehouse ?? x.WarehouseName,
                    x.CurrentQuantity.ToString("0.##"),
                    x.SafetyStock.ToString("0.##"),
                    x.StockStatus ?? "",
                    x.CostMethod ?? "",
                    x.CostPrice.ToString("0.00"),
                    x.StockAmount.ToString("0.00")
                }.Select(Csv)));
            }
            WriteCsvDownload(ctx, BuildExportFileName("库存汇总"), sb.ToString());
        }

        struct BackupFileSpec
        {
            public string Path;
            public string FileName;
        }

        static BackupFileSpec[] GetAllBackupFileSpecs()
        {
            return new[]
            {
                new BackupFileSpec { Path = UsersFile, FileName = "users.json" },
                new BackupFileSpec { Path = SystemSettingsFile, FileName = "system_settings.json" },
                new BackupFileSpec { Path = DictionaryOptionsFile, FileName = "dictionary_options.json" },
                new BackupFileSpec { Path = ContractSettingsFile, FileName = "contract_settings.json" },
                new BackupFileSpec { Path = ContractSettingSequenceFile, FileName = "contract_setting_sequence.json" },
                new BackupFileSpec { Path = DataFile, FileName = "suppliers.json" },
                new BackupFileSpec { Path = SupplierSequenceFile, FileName = "supplier_sequence.json" },
                new BackupFileSpec { Path = CustomerFile, FileName = "customers.json" },
                new BackupFileSpec { Path = CustomerSequenceFile, FileName = "customer_sequence.json" },
                new BackupFileSpec { Path = MaterialFile, FileName = "materials.json" },
                new BackupFileSpec { Path = MaterialSequenceFile, FileName = "material_sequence.json" },
                new BackupFileSpec { Path = FinanceFile, FileName = "finance.json" },
                new BackupFileSpec { Path = OpeningFile, FileName = "finance_opening.json" },
                new BackupFileSpec { Path = BomFile, FileName = "bom.json" },
                new BackupFileSpec { Path = BomSequenceFile, FileName = "bom_sequence.json" },
                new BackupFileSpec { Path = ModelCostFile, FileName = "model_costs.json" },
                new BackupFileSpec { Path = ContractsFile, FileName = "contracts.json" },
                new BackupFileSpec { Path = ContractSequenceFile, FileName = "contract_sequence.json" },
                new BackupFileSpec { Path = SalesOrdersFile, FileName = "sales_orders.json" },
                new BackupFileSpec { Path = SalesOrderSequenceFile, FileName = "sales_order_sequence.json" },
                new BackupFileSpec { Path = SalesOutboundsFile, FileName = "sales_outbounds.json" },
                new BackupFileSpec { Path = SalesOutboundSequenceFile, FileName = "sales_outbound_sequence.json" },
                new BackupFileSpec { Path = PurchaseOrdersFile, FileName = "purchase_orders.json" },
                new BackupFileSpec { Path = PurchaseOrderSequenceFile, FileName = "purchase_order_sequence.json" },
                new BackupFileSpec { Path = PurchaseInboundsFile, FileName = "purchase_inbounds.json" },
                new BackupFileSpec { Path = PurchaseInboundSequenceFile, FileName = "purchase_inbound_sequence.json" },
                new BackupFileSpec { Path = ProductionPicksFile, FileName = "production_picks.json" },
                new BackupFileSpec { Path = ProductionPickSequenceFile, FileName = "production_pick_sequence.json" },
                new BackupFileSpec { Path = FinishedInboundsFile, FileName = "finished_inbounds.json" },
                new BackupFileSpec { Path = FinishedInboundSequenceFile, FileName = "finished_inbound_sequence.json" },
                new BackupFileSpec { Path = ReceivablesFile, FileName = "receivables.json" },
                new BackupFileSpec { Path = ReceivableSequenceFile, FileName = "receivable_sequence.json" },
                new BackupFileSpec { Path = PayablesFile, FileName = "payables.json" },
                new BackupFileSpec { Path = PayableSequenceFile, FileName = "payable_sequence.json" },
                new BackupFileSpec { Path = LogFile, FileName = "operation.log" }
            };
        }

        static string BackupFilesToFolder(string folder, BackupFileSpec[] specs)
        {
            Directory.CreateDirectory(folder);
            foreach (var spec in specs)
            {
                if (!File.Exists(spec.Path)) continue;
                File.Copy(spec.Path, Path.Combine(folder, spec.FileName), true);
            }
            return folder;
        }

        static void EnsureBackupSucceeded(string folder, BackupFileSpec[] specs)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new BusinessException("备份失败，禁止继续清空数据", 409);
            int expected = specs.Count(s => File.Exists(s.Path));
            int copied = Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;
            if (expected > 0 && copied == 0)
                throw new BusinessException("备份失败，未生成备份文件，禁止继续清空数据", 409);
        }

        static void RestoreFilesFromFolder(string folder, BackupFileSpec[] specs)
        {
            foreach (var spec in specs)
            {
                string src = Path.Combine(folder, spec.FileName);
                if (!File.Exists(src)) continue;
                File.Copy(src, spec.Path, true);
            }
        }

        struct ClearDataFileSpec
        {
            public string Path;
            public string FileName;
            public string EmptyContent;
        }

        static ClearDataFileSpec[] GetClearTestDataFileSpecs()
        {
            return new[]
            {
                new ClearDataFileSpec { Path = DataFile, FileName = "suppliers.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = SupplierSequenceFile, FileName = "supplier_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = CustomerFile, FileName = "customers.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = CustomerSequenceFile, FileName = "customer_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = MaterialFile, FileName = "materials.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = MaterialSequenceFile, FileName = "material_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = FinanceFile, FileName = "finance.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = OpeningFile, FileName = "finance_opening.json", EmptyContent = Json.Serialize(new OpeningBalances()) },
                new ClearDataFileSpec { Path = BomFile, FileName = "bom.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = BomSequenceFile, FileName = "bom_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = ModelCostFile, FileName = "model_costs.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = ModelCostSequenceFile, FileName = "model_cost_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = ContractsFile, FileName = "contracts.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = ContractSequenceFile, FileName = "contract_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = SalesOrdersFile, FileName = "sales_orders.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = SalesOrderSequenceFile, FileName = "sales_order_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = SalesOutboundsFile, FileName = "sales_outbounds.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = SalesOutboundSequenceFile, FileName = "sales_outbound_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = PurchaseOrdersFile, FileName = "purchase_orders.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = PurchaseOrderSequenceFile, FileName = "purchase_order_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = PurchaseInboundsFile, FileName = "purchase_inbounds.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = PurchaseInboundSequenceFile, FileName = "purchase_inbound_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = ProductionPicksFile, FileName = "production_picks.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = ProductionPickSequenceFile, FileName = "production_pick_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = FinishedInboundsFile, FileName = "finished_inbounds.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = FinishedInboundSequenceFile, FileName = "finished_inbound_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = ReceivablesFile, FileName = "receivables.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = ReceivableSequenceFile, FileName = "receivable_sequence.json", EmptyContent = "0" },
                new ClearDataFileSpec { Path = PayablesFile, FileName = "payables.json", EmptyContent = "[]" },
                new ClearDataFileSpec { Path = PayableSequenceFile, FileName = "payable_sequence.json", EmptyContent = "0" }
            };
        }

        static string BackupBeforeClearTestData(BackupFileSpec[] specs)
        {
            string folderName = "backup_before_clear_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = Path.Combine(BackupDir, folderName);
            return BackupFilesToFolder(folder, specs);
        }

        static string ManualBackup()
        {
            lock (DataLock)
            {
                Directory.CreateDirectory(BackupDir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string folder = Path.Combine(BackupDir, "manual_backup_" + stamp);
                BackupFilesToFolder(folder, GetAllBackupFileSpecs());
                CleanBackups();
                return folder;
            }
        }

        static void CleanBackups()
        {
            foreach (var f in new DirectoryInfo(BackupDir).GetFiles("*.json").OrderByDescending(x=>x.CreationTime).Skip(50)) try { f.Delete(); } catch { }
        }

        static string ReadBody(HttpListenerRequest req) { using (var sr = new StreamReader(req.InputStream, Encoding.UTF8)) return sr.ReadToEnd(); }
        static void WriteJson(HttpListenerContext ctx, object obj, int status) { byte[] b=Encoding.UTF8.GetBytes(Json.Serialize(obj)); ctx.Response.StatusCode=status; ctx.Response.ContentType="application/json; charset=utf-8"; ctx.Response.ContentLength64=b.Length; ctx.Response.OutputStream.Write(b,0,b.Length); ctx.Response.Close(); }
        static void WriteJson(HttpListenerContext ctx, object obj) { WriteJson(ctx, obj, 200); }
        static void AddSecurityHeaders(HttpListenerResponse r) { r.AddHeader("X-Content-Type-Options","nosniff"); r.AddHeader("X-Frame-Options","DENY"); r.AddHeader("Cache-Control","no-store"); }
        static string Csv(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
        static string Sha256(string value) { using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-","").ToLowerInvariant(); }
        static bool FixedEquals(string a, string b) { if (a==null||b==null||a.Length!=b.Length) return false; int d=0; for(int i=0;i<a.Length;i++) d|=a[i]^b[i]; return d==0; }
        static string GetLanIp()
        {
            try { foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (ip.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString(); } catch { }
            return "127.0.0.1";
        }
    }
}
