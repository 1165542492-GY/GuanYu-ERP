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
            "contract.view","contract.add","contract.edit","contract.delete","contract.preview","contract.print","contract.void",
            "contract_setting.view","contract_setting.add","contract_setting.edit","contract_setting.delete",
            "settings.view","settings.account","settings.password","settings.tax_rate","settings.dictionary",
            "sales_order.view","sales_order.add","sales_order.edit","sales_order.delete","sales_order.import","sales_order.export",
            "sales_outbound.view","sales_outbound.add","sales_outbound.edit","sales_outbound.delete","sales_outbound.import","sales_outbound.export",
            "purchase_order.view","purchase_order.add","purchase_order.edit","purchase_order.delete","purchase_order.import","purchase_order.export",
            "purchase_inbound.view","purchase_inbound.add","purchase_inbound.edit","purchase_inbound.delete","purchase_inbound.import","purchase_inbound.export",
            "production_pick.view","production_pick.add","production_pick.edit","production_pick.delete",
            "finished_inbound.view","finished_inbound.add","finished_inbound.edit","finished_inbound.delete",
            "stock.view",
            "receivable.view","receivable.add","receivable.edit","receivable.delete",
            "receivable.receipt_add","receivable.receipt_edit","receivable.receipt_delete",
            "payable.view","payable.add","payable.edit","payable.delete",
            "payable.payment_add","payable.payment_edit","payable.payment_delete",
            "reconciliation.customer_view","reconciliation.customer_export",
            "after_sales.view","after_sales.add","after_sales.edit","after_sales.delete"
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
            { "purchase_order.import", new[] { "purchase_order.add" } },
            { "purchase_inbound.import", new[] { "purchase_inbound.add" } },
            { "receivable.receipt_add", new[] { "receivable.edit" } },
            { "receivable.receipt_edit", new[] { "receivable.edit" } },
            { "receivable.receipt_delete", new[] { "receivable.edit" } },
            { "payable.payment_add", new[] { "payable.edit" } },
            { "payable.payment_edit", new[] { "payable.edit" } },
            { "payable.payment_delete", new[] { "payable.edit" } },
            { "contract.void", new[] { "contract.edit" } }
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
                new PermissionGroup { Module = "合同管理", Items = new[] {
                    new PermissionItem { Key = "contract.view", Label = "查看合同" },
                    new PermissionItem { Key = "contract.add", Label = "新增合同" },
                    new PermissionItem { Key = "contract.edit", Label = "修改合同" },
                    new PermissionItem { Key = "contract.delete", Label = "删除合同" },
                    new PermissionItem { Key = "contract.preview", Label = "预览合同" },
                    new PermissionItem { Key = "contract.print", Label = "打印合同" },
                    new PermissionItem { Key = "contract.void", Label = "作废合同" }
                }},
                new PermissionGroup { Module = "合同资料", Items = new[] {
                    new PermissionItem { Key = "contract_setting.view", Label = "查看" },
                    new PermissionItem { Key = "contract_setting.add", Label = "新增" },
                    new PermissionItem { Key = "contract_setting.edit", Label = "修改" },
                    new PermissionItem { Key = "contract_setting.delete", Label = "删除" }
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
                    new PermissionItem { Key = "sales_outbound.export", Label = "导出" }
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
                new PermissionGroup { Module = "库存汇总", Items = new[] {
                    new PermissionItem { Key = "stock.view", Label = "查看" }
                }},
                new PermissionGroup { Module = "售后维修工单", Items = new[] {
                    new PermissionItem { Key = "after_sales.view", Label = "查看" },
                    new PermissionItem { Key = "after_sales.add", Label = "新增" },
                    new PermissionItem { Key = "after_sales.edit", Label = "修改" },
                    new PermissionItem { Key = "after_sales.delete", Label = "删除" }
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
