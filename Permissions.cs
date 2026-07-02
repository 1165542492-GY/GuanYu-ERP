using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public static partial class Program
    {
        static readonly string[] AllPermissionKeys = {
            "supplier.view","supplier.add","supplier.edit","supplier.delete","supplier.batch_delete","supplier.import","supplier.export",
            "customer.view","customer.add","customer.edit","customer.delete","customer.batch_delete","customer.import","customer.export",
            "material.view","material.add","material.edit","material.delete","material.batch_delete","material.import","material.export",
            "finance.view","finance.add","finance.edit","finance.delete","finance.import","finance.export","finance.opening_view","finance.opening_edit",
            "bom.view","bom.add","bom.edit","bom.delete","bom.export","bom.import",
            "model_cost.view","model_cost.add","model_cost.edit","model_cost.delete","model_cost.export","model_cost.import",
            "settings.view","settings.account","settings.password","settings.tax_rate","settings.dictionary",
            "sales_order.view","sales_order.add","sales_order.edit","sales_order.delete","sales_order.import","sales_order.export",
            "sales_outbound.view","sales_outbound.add","sales_outbound.edit","sales_outbound.delete","sales_outbound.import","sales_outbound.export",
            "sales_outbound_detail.view","sales_outbound_detail.add","sales_outbound_detail.edit","sales_outbound_detail.delete",
            "sales_outbound_accessory.view","sales_outbound_accessory.add","sales_outbound_accessory.edit","sales_outbound_accessory.delete",
            "sales_outbound.adjust_quantity","sales_outbound.adjust_reason_view",
            "purchase_order.view","purchase_order.add","purchase_order.edit","purchase_order.delete","purchase_order.import","purchase_order.export",
            "purchase_inbound.view","purchase_inbound.add","purchase_inbound.edit","purchase_inbound.delete","purchase_inbound.import","purchase_inbound.export",
            "production_work_order.view","production_work_order.add","production_work_order.edit","production_work_order.delete","production_work_order.status",
            "production_pick.view","production_pick.add","production_pick.edit","production_pick.delete",
            "finished_inbound.view","finished_inbound.add","finished_inbound.edit","finished_inbound.delete",
            "stock.view","stock_summary.view","inventory_movement.view","stock_detail.view","source_trace.view","inventory_protection.view",
            "receivable.view","receivable.add","receivable.edit","receivable.delete",
            "receivable.receipt_add","receivable.receipt_edit","receivable.receipt_delete",
            "payable.view","payable.add","payable.edit","payable.delete",
            "payable.payment_add","payable.payment_edit","payable.payment_delete",
            "reconciliation.customer_view","reconciliation.customer_export",
            "after_sales.view","after_sales.add","after_sales.edit","after_sales.delete","after_sales.cancel",
            "after_sales.parts_view","after_sales.pick","after_sales.return","after_sales.extra_pick","after_sales.final_usage",
            "operation_log.view","operation_log.query","operation_log.export",
            "test_data.export","test_data.import_preview","test_data.import_run","test_data.import_result_view","test_data.export_result_view"
        };

        static readonly Dictionary<string, string[]> PermissionLegacyMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "supplier.import", new[] { "supplier.add" } },
            { "customer.import", new[] { "customer.add" } },
            { "material.import", new[] { "material.add" } },
            { "finance.import", new[] { "finance.add" } },
            { "finance.opening_view", new[] { "finance.view" } },
            { "finance.opening_edit", new[] { "finance.edit" } },
            { "sales_order.import", new[] { "sales_order.add" } },
            { "sales_outbound.import", new[] { "sales_outbound.add" } },
            { "sales_outbound_detail.view", new[] { "sales_outbound.view" } },
            { "sales_outbound_detail.add", new[] { "sales_outbound.add" } },
            { "sales_outbound_detail.edit", new[] { "sales_outbound.edit" } },
            { "sales_outbound_detail.delete", new[] { "sales_outbound.delete" } },
            { "sales_outbound_accessory.view", new[] { "sales_outbound.view" } },
            { "sales_outbound_accessory.add", new[] { "sales_outbound.add" } },
            { "sales_outbound_accessory.edit", new[] { "sales_outbound.edit" } },
            { "sales_outbound_accessory.delete", new[] { "sales_outbound.delete" } },
            { "sales_outbound.adjust_quantity", new[] { "sales_outbound.edit" } },
            { "sales_outbound.adjust_reason_view", new[] { "sales_outbound.view" } },
            { "purchase_order.import", new[] { "purchase_order.add" } },
            { "purchase_inbound.import", new[] { "purchase_inbound.add" } },
            { "production_work_order.view", new[] { "production_pick.view" } },
            { "production_work_order.add", new[] { "production_pick.add" } },
            { "production_work_order.edit", new[] { "production_pick.edit" } },
            { "production_work_order.delete", new[] { "production_pick.delete" } },
            { "production_work_order.status", new[] { "production_pick.edit" } },
            { "stock_summary.view", new[] { "stock.view" } },
            { "inventory_movement.view", new[] { "stock.view" } },
            { "stock_detail.view", new[] { "stock.view" } },
            { "source_trace.view", new[] { "stock.view" } },
            { "inventory_protection.view", new[] { "stock.view", "operation_log.view" } },
            { "receivable.receipt_add", new[] { "receivable.edit" } },
            { "receivable.receipt_edit", new[] { "receivable.edit" } },
            { "receivable.receipt_delete", new[] { "receivable.edit" } },
            { "payable.payment_add", new[] { "payable.edit" } },
            { "payable.payment_edit", new[] { "payable.edit" } },
            { "payable.payment_delete", new[] { "payable.edit" } },
            { "after_sales.cancel", new[] { "after_sales.edit" } },
            { "after_sales.parts_view", new[] { "after_sales.view" } },
            { "after_sales.pick", new[] { "after_sales.edit" } },
            { "after_sales.return", new[] { "after_sales.edit" } },
            { "after_sales.extra_pick", new[] { "after_sales.edit" } },
            { "after_sales.final_usage", new[] { "after_sales.view" } },
            { "test_data.import_preview", new[] { "test_data.import_run" } },
            { "test_data.import_result_view", new[] { "test_data.import_preview", "test_data.import_run" } },
            { "test_data.export_result_view", new[] { "test_data.export" } }
        };

        static string[] NormalizePermissions(string[] permissions)
        {
            if (permissions == null || permissions.Length == 0) return new string[0];
            return permissions.Where(p => !string.IsNullOrWhiteSpace(p) && AllPermissionKeys.Contains(p)).Distinct().ToArray();
        }

        static bool HasPermission(UserSession user, string permission)
        {
            if (user == null || string.IsNullOrWhiteSpace(permission)) return false;
            if (user.IsAdmin) return true;
            if (user.Permissions == null) return false;
            if (user.Permissions.Contains(permission)) return true;
            string[] legacy;
            if (PermissionLegacyMap.TryGetValue(permission, out legacy))
            {
                foreach (var oldKey in legacy)
                {
                    if (user.Permissions.Contains(oldKey)) return true;
                }
            }
            return false;
        }

        static bool RequirePermission(HttpListenerContext ctx, UserSession user, string permission)
        {
            if (HasPermission(user, permission)) return true;
            WriteJson(ctx, new { error = "无权限操作" }, 403);
            return false;
        }

        static bool RequireAdmin(HttpListenerContext ctx, UserSession user)
        {
            if (IsAdminUser(user)) return true;
            WriteJson(ctx, new { error = "无权限操作" }, 403);
            return false;
        }

        static bool CanReadDictionaryOptions(UserSession user)
        {
            return user != null;
        }

        static PermissionGroup[] GetPermissionDefinitions()
        {
            return new[]
            {
                new PermissionGroup { Module = "供应商管理", Items = new[] {
                    new PermissionItem { Key = "supplier.view", Label = "查看" },
                    new PermissionItem { Key = "supplier.add", Label = "新增" },
                    new PermissionItem { Key = "supplier.edit", Label = "修改" },
                    new PermissionItem { Key = "supplier.delete", Label = "删除" },
                    new PermissionItem { Key = "supplier.batch_delete", Label = "批量删除" },
                    new PermissionItem { Key = "supplier.import", Label = "导入" },
                    new PermissionItem { Key = "supplier.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "客户管理", Items = new[] {
                    new PermissionItem { Key = "customer.view", Label = "查看" },
                    new PermissionItem { Key = "customer.add", Label = "新增" },
                    new PermissionItem { Key = "customer.edit", Label = "修改" },
                    new PermissionItem { Key = "customer.delete", Label = "删除" },
                    new PermissionItem { Key = "customer.batch_delete", Label = "批量删除" },
                    new PermissionItem { Key = "customer.import", Label = "导入" },
                    new PermissionItem { Key = "customer.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "物料管理", Items = new[] {
                    new PermissionItem { Key = "material.view", Label = "查看" },
                    new PermissionItem { Key = "material.add", Label = "新增" },
                    new PermissionItem { Key = "material.edit", Label = "修改" },
                    new PermissionItem { Key = "material.delete", Label = "删除" },
                    new PermissionItem { Key = "material.batch_delete", Label = "批量删除" },
                    new PermissionItem { Key = "material.import", Label = "导入" },
                    new PermissionItem { Key = "material.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "财务收支", Items = new[] {
                    new PermissionItem { Key = "finance.view", Label = "查看收支" },
                    new PermissionItem { Key = "finance.add", Label = "新增收支" },
                    new PermissionItem { Key = "finance.edit", Label = "修改收支" },
                    new PermissionItem { Key = "finance.delete", Label = "删除收支" },
                    new PermissionItem { Key = "finance.import", Label = "导入收支" },
                    new PermissionItem { Key = "finance.export", Label = "导出收支" },
                    new PermissionItem { Key = "finance.opening_view", Label = "查看期初余额" },
                    new PermissionItem { Key = "finance.opening_edit", Label = "修改期初余额" }
                }},
                new PermissionGroup { Module = "BOM表", Items = new[] {
                    new PermissionItem { Key = "bom.view", Label = "查看" },
                    new PermissionItem { Key = "bom.add", Label = "新增" },
                    new PermissionItem { Key = "bom.edit", Label = "修改" },
                    new PermissionItem { Key = "bom.delete", Label = "删除" },
                    new PermissionItem { Key = "bom.export", Label = "导出" },
                    new PermissionItem { Key = "bom.import", Label = "导入" }
                }},
                new PermissionGroup { Module = "机型成本", Items = new[] {
                    new PermissionItem { Key = "model_cost.view", Label = "查看" },
                    new PermissionItem { Key = "model_cost.add", Label = "新增" },
                    new PermissionItem { Key = "model_cost.edit", Label = "修改" },
                    new PermissionItem { Key = "model_cost.delete", Label = "删除" },
                    new PermissionItem { Key = "model_cost.export", Label = "导出" },
                    new PermissionItem { Key = "model_cost.import", Label = "导入" }
                }},
                new PermissionGroup { Module = "销售订单", Items = new[] {
                    new PermissionItem { Key = "sales_order.view", Label = "查看" },
                    new PermissionItem { Key = "sales_order.add", Label = "新增" },
                    new PermissionItem { Key = "sales_order.edit", Label = "修改" },
                    new PermissionItem { Key = "sales_order.delete", Label = "删除" },
                    new PermissionItem { Key = "sales_order.import", Label = "导入" },
                    new PermissionItem { Key = "sales_order.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "销售出库", Items = new[] {
                    new PermissionItem { Key = "sales_outbound.view", Label = "查看" },
                    new PermissionItem { Key = "sales_outbound.add", Label = "新增" },
                    new PermissionItem { Key = "sales_outbound.edit", Label = "修改" },
                    new PermissionItem { Key = "sales_outbound.delete", Label = "删除" },
                    new PermissionItem { Key = "sales_outbound.import", Label = "导入" },
                    new PermissionItem { Key = "sales_outbound.export", Label = "导出" },
                    new PermissionItem { Key = "sales_outbound_detail.view", Label = "查看出库明细" },
                    new PermissionItem { Key = "sales_outbound_detail.add", Label = "新增出库明细" },
                    new PermissionItem { Key = "sales_outbound_detail.edit", Label = "修改出库明细" },
                    new PermissionItem { Key = "sales_outbound_detail.delete", Label = "删除出库明细" },
                    new PermissionItem { Key = "sales_outbound_accessory.view", Label = "查看销售赠品/随货配件" },
                    new PermissionItem { Key = "sales_outbound_accessory.add", Label = "新增销售赠品/随货配件" },
                    new PermissionItem { Key = "sales_outbound_accessory.edit", Label = "修改销售赠品/随货配件" },
                    new PermissionItem { Key = "sales_outbound_accessory.delete", Label = "删除销售赠品/随货配件" },
                    new PermissionItem { Key = "sales_outbound.adjust_quantity", Label = "实际出库数量调整" },
                    new PermissionItem { Key = "sales_outbound.adjust_reason_view", Label = "调整原因查看" }
                }},
                new PermissionGroup { Module = "采购单", Items = new[] {
                    new PermissionItem { Key = "purchase_order.view", Label = "查看" },
                    new PermissionItem { Key = "purchase_order.add", Label = "新增" },
                    new PermissionItem { Key = "purchase_order.edit", Label = "修改" },
                    new PermissionItem { Key = "purchase_order.delete", Label = "删除" },
                    new PermissionItem { Key = "purchase_order.import", Label = "导入" },
                    new PermissionItem { Key = "purchase_order.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "采购入库", Items = new[] {
                    new PermissionItem { Key = "purchase_inbound.view", Label = "查看" },
                    new PermissionItem { Key = "purchase_inbound.add", Label = "新增" },
                    new PermissionItem { Key = "purchase_inbound.edit", Label = "修改" },
                    new PermissionItem { Key = "purchase_inbound.delete", Label = "删除" },
                    new PermissionItem { Key = "purchase_inbound.import", Label = "导入" },
                    new PermissionItem { Key = "purchase_inbound.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "生产工单", Items = new[] {
                    new PermissionItem { Key = "production_work_order.view", Label = "查看" },
                    new PermissionItem { Key = "production_work_order.add", Label = "新增" },
                    new PermissionItem { Key = "production_work_order.edit", Label = "修改" },
                    new PermissionItem { Key = "production_work_order.delete", Label = "删除/取消" },
                    new PermissionItem { Key = "production_work_order.status", Label = "状态变更/确认" }
                }},
                new PermissionGroup { Module = "生产领用", Items = new[] {
                    new PermissionItem { Key = "production_pick.view", Label = "查看" },
                    new PermissionItem { Key = "production_pick.add", Label = "新增" },
                    new PermissionItem { Key = "production_pick.edit", Label = "修改" },
                    new PermissionItem { Key = "production_pick.delete", Label = "删除" }
                }},
                new PermissionGroup { Module = "成品入库", Items = new[] {
                    new PermissionItem { Key = "finished_inbound.view", Label = "查看" },
                    new PermissionItem { Key = "finished_inbound.add", Label = "新增" },
                    new PermissionItem { Key = "finished_inbound.edit", Label = "修改" },
                    new PermissionItem { Key = "finished_inbound.delete", Label = "删除" }
                }},
                new PermissionGroup { Module = "库存管理", Items = new[] {
                    new PermissionItem { Key = "stock.view", Label = "库存兼容查看" },
                    new PermissionItem { Key = "stock_summary.view", Label = "库存汇总查看" },
                    new PermissionItem { Key = "inventory_movement.view", Label = "库存流水查看" },
                    new PermissionItem { Key = "stock_detail.view", Label = "库存详情查看" },
                    new PermissionItem { Key = "source_trace.view", Label = "来源追溯查看" },
                    new PermissionItem { Key = "inventory_protection.view", Label = "库存保护拦截记录查看" }
                }},
                new PermissionGroup { Module = "售后维修工单", Items = new[] {
                    new PermissionItem { Key = "after_sales.view", Label = "查看" },
                    new PermissionItem { Key = "after_sales.add", Label = "新增" },
                    new PermissionItem { Key = "after_sales.edit", Label = "修改" },
                    new PermissionItem { Key = "after_sales.delete", Label = "删除" },
                    new PermissionItem { Key = "after_sales.cancel", Label = "取消" },
                    new PermissionItem { Key = "after_sales.parts_view", Label = "维修物料明细查看" },
                    new PermissionItem { Key = "after_sales.pick", Label = "维修领料" },
                    new PermissionItem { Key = "after_sales.return", Label = "维修退料" },
                    new PermissionItem { Key = "after_sales.extra_pick", Label = "维修补领" },
                    new PermissionItem { Key = "after_sales.final_usage", Label = "维修最终耗用查看" }
                }},
                new PermissionGroup { Module = "操作记录", Items = new[] {
                    new PermissionItem { Key = "operation_log.view", Label = "查看" },
                    new PermissionItem { Key = "operation_log.query", Label = "筛选/查询" },
                    new PermissionItem { Key = "operation_log.export", Label = "导出" }
                }},
                new PermissionGroup { Module = "测试总表导入导出", Items = new[] {
                    new PermissionItem { Key = "test_data.export", Label = "测试总表导出" },
                    new PermissionItem { Key = "test_data.import_preview", Label = "预检查导入" },
                    new PermissionItem { Key = "test_data.import_run", Label = "正式导入" },
                    new PermissionItem { Key = "test_data.import_result_view", Label = "导入结果查看" },
                    new PermissionItem { Key = "test_data.export_result_view", Label = "导出结果查看" }
                }},
                new PermissionGroup { Module = "应收款", Items = new[] {
                    new PermissionItem { Key = "receivable.view", Label = "查看" },
                    new PermissionItem { Key = "receivable.add", Label = "新增" },
                    new PermissionItem { Key = "receivable.edit", Label = "修改" },
                    new PermissionItem { Key = "receivable.delete", Label = "删除" },
                    new PermissionItem { Key = "receivable.receipt_add", Label = "收款明细新增" },
                    new PermissionItem { Key = "receivable.receipt_edit", Label = "收款明细修改" },
                    new PermissionItem { Key = "receivable.receipt_delete", Label = "收款明细删除" }
                }},
                new PermissionGroup { Module = "应付款", Items = new[] {
                    new PermissionItem { Key = "payable.view", Label = "查看" },
                    new PermissionItem { Key = "payable.add", Label = "新增" },
                    new PermissionItem { Key = "payable.edit", Label = "修改" },
                    new PermissionItem { Key = "payable.delete", Label = "删除" },
                    new PermissionItem { Key = "payable.payment_add", Label = "付款明细新增" },
                    new PermissionItem { Key = "payable.payment_edit", Label = "付款明细修改" },
                    new PermissionItem { Key = "payable.payment_delete", Label = "付款明细删除" }
                }},
                new PermissionGroup { Module = "客户对账单", Items = new[] {
                    new PermissionItem { Key = "reconciliation.customer_view", Label = "查看" },
                    new PermissionItem { Key = "reconciliation.customer_export", Label = "导出 Excel" }
                }},
                new PermissionGroup { Module = "系统设置（仅管理员）", Items = new[] {
                    new PermissionItem { Key = "settings.view", Label = "查看系统设置（管理员）" },
                    new PermissionItem { Key = "settings.account", Label = "账号管理（管理员）" },
                    new PermissionItem { Key = "settings.password", Label = "修改密码" },
                    new PermissionItem { Key = "settings.tax_rate", Label = "税率修改（管理员）" },
                    new PermissionItem { Key = "settings.dictionary", Label = "字典选项（管理员）" }
                }}
            };
        }
    }
}
