﻿using Dapper;
using Microsoft.Extensions.Configuration;
using Misa.ApplicationCore.Attributes;
using Misa.ApplicationCore.Entities;
using Misa.ApplicationCore.Interfaces.Repository;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misa.Infrastructure
{
    public class AccountVoucherRepository : BaseRepository<AccountVoucher>, IAccountVoucherRepository
    {
        #region Constructor
        public AccountVoucherRepository(IConfiguration configuration) : base(configuration)
        {

        }


        #endregion

        /// <summary>
        /// lọc và phân trang chứng từ
        /// </summary>
        /// <param name="searchData">Từ khóa tìm kiếm</param>
        /// <param name="mentionState">Trạng thái ghi sổ</param>
        /// <param name="voucherType">loại chứng từ</param>
        /// <param name="startDate">ngày lọc đầu</param>
        /// <param name="endDate">ngày lọc cuối</param>
        /// <param name="pageIndex">index trang</param>
        /// <param name="pageSize">số bản ghi trên trang</param>
        /// <returns></returns>
        /// CreatedBy: nvdien(24/9/2021)
        public object getAccountVoucherPagingFilter(string searchData, int? mentionState, string voucherType, DateTime? startDate, DateTime? endDate, int pageIndex, int pageSize)
        {
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {
                DynamicParameters dynamicParameters = new DynamicParameters();
                var voucherFilter = searchData == null ? string.Empty : searchData;

                var typeVoucher = voucherType == null ? string.Empty : voucherType;
                dynamicParameters.Add("@search_data", voucherFilter);
                dynamicParameters.Add("@mention_state", mentionState);
                dynamicParameters.Add("@type_voucher", typeVoucher);
                dynamicParameters.Add("@start_date", startDate);
                dynamicParameters.Add("@end_date", endDate);
                dynamicParameters.Add("@offset", (pageIndex - 1) * pageSize);
                dynamicParameters.Add("@page_size", pageSize);

                var sql = "select * from  public.func_get_voucher_paging_filter(@search_data, @mention_state,@type_voucher, @start_date, @end_date) limit @page_size offset @offset;";
                sql += "select count(*) from (select * from  public.func_get_voucher_paging_filter(@search_data, @mention_state, @type_voucher,  @start_date, @end_date)) as filtertable;";
                sql += "select coalesce(sum(filtertable.total_price),0) as total_prices from (select * from  public.func_get_voucher_paging_filter(@search_data, @mention_state, @type_voucher,  @start_date, @end_date)) as filtertable;";
                var response = _dbConnection.QueryMultiple(sql, param: dynamicParameters, commandType: CommandType.Text);

                //var vmodel = Activator.CreateInstance<Employee>();
                var vouchers = response.Read<AccountVoucher>();
                var totalRecord = response.Read<int>().FirstOrDefault();
                var totalPrices = response.Read<decimal>().FirstOrDefault();
                var totalPage = Math.Ceiling((double)totalRecord / pageSize);
                var result = new
                {
                    Vouchers = vouchers,
                    TotalPrices = totalPrices,
                    TotalRecord = totalRecord,
                    TotalPage = totalPage,

                };
                return result;
            }
        }

        /// <summary>
        /// Lấy chi tiết chứng từ theo Id
        /// </summary>
        /// <param name="accountVoucherID">ID chứng từ</param>
        /// <returns></returns>
        /// CreatedBy: nvdien(24/9/2021)
        public object getAccountVoucherDetail(Guid accountVoucherID)
        {
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {
                DynamicParameters dynamicParameters = new DynamicParameters();

                dynamicParameters.Add("@accountvoucher_id", accountVoucherID);
                var sql = "select * from public.view_accountvoucher_accountobject av where av.accountvoucher_id = @accountvoucher_id;";
                sql += "select * from public.view_accountvoucherdetail_commodity ac where ac.accountvoucher_id = @accountvoucher_id order by ac.created_date asc;";

                var response = _dbConnection.QueryMultiple(sql, param: dynamicParameters, commandType: CommandType.Text);

                var vouchers = response.Read<AccountVoucher>();
                var voucherDetails = response.Read<AccountVoucherDetail>();
                var inwardDetail = new List<object>();

                if (voucherDetails.Count() > 0)
                {
                    foreach (var item in voucherDetails)
                    {
                        var commodityId = item.commodity_id;
                        DynamicParameters dynamicParameters1 = new DynamicParameters();

                        dynamicParameters1.Add("@commodity_id", commodityId);
                        var sql2 = "select * from view_commodity_unit vcu2 where vcu2.commodity_id = @commodity_id order by vcu2.is_main_unit desc";
                        var units = _dbConnection.Query<CommodityUnit>(sql2, param: dynamicParameters1, commandType: CommandType.Text);
                        var unitList = JsonConvert.SerializeObject(units);
                        item.units = String.Join(",", unitList);
                        inwardDetail.Add(item);
                    }
                }

                var result = new
                {
                    Data = new
                    {
                        in_inward = vouchers,
                        in_inward_detail = inwardDetail,
                    }

                };
                return result;
            }
        }

        /// <summary>
        /// Ghi sổ nhiều
        /// </summary>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        /// CreatedBy: nvdien(28/09/2021)
        public int mentionMany(List<Guid> entityIds)
        {
            var parameters = new DynamicParameters();
            var paramName = new List<string>();

            for (int i = 0; i < entityIds.Count; i++)
            {
                var id = entityIds[i];
                // Đặt tên cho param
                paramName.Add($"@m_Id{i}");
                // Đặt giá trị cho param bằng id 
                parameters.Add($"@m_Id{i}", id);
            }
            // Join mảng để tạo ra câu truy vấn ghi sổ nhiều
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {
                var sqlCommand = $"UPDATE accountvoucher SET is_mention = 1 WHERE accountvoucher_id IN ({String.Join(", ", paramName.ToArray())})";
                var rowEffects = _dbConnection.Execute(sqlCommand, param: parameters);
                return rowEffects;
            }
        }

        /// <summary>
        /// Bỏ ghi nhiều
        /// </summary>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        /// CreatedBy: nvdien(28/09/2021)
        public int unMentionMany(List<Guid> entityIds)
        {
            var parameters = new DynamicParameters();
            var paramName = new List<string>();

            for (int i = 0; i < entityIds.Count; i++)
            {
                var id = entityIds[i];
                // Đặt tên cho param
                paramName.Add($"@m_Id{i}");
                // Đặt giá trị cho param bằng id 
                parameters.Add($"@m_Id{i}", id);
            }
            // Join mảng để tạo ra câu truy vấn ghi sổ nhiều
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {
                var sqlCommand = $"UPDATE accountvoucher SET is_mention = 0 WHERE accountvoucher_id IN ({String.Join(", ", paramName.ToArray())})";
                var rowEffects = _dbConnection.Execute(sqlCommand, param: parameters);
                return rowEffects;
            }
        }
        /// <summary>
        /// Lấy mã chứng từ mới
        /// </summary>
        /// <returns></returns>
        public AccountVoucher getNewVoucherCode()
        {
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {


                var sqlCommand = "select * from public.accountvoucher av order by cast( public.func_extract_number(av.voucher_code) as int) DESC LIMIT 1";
                var voucher = _dbConnection.Query<AccountVoucher>(sqlCommand).Single();
                return voucher;
            }
        }
        public object addAccountVoucher(AccountVoucher data)
        {
            using (_dbConnection = new NpgsqlConnection(_connectionString))
            {
                DynamicParameters dynamicParameters = new DynamicParameters();
                var properties = data.GetType().GetProperties();
                foreach (var property in properties)
                {
                    if (property.IsDefined(typeof(MisaNotMap), false)) continue;
                    var propName = property.Name;
                    var propValue = property.GetValue(data);
                    dynamicParameters.Add($"@{propName}_insert", propValue);

                }
                var proceduce = $"func_insert_test";
                var reader = _dbConnection.ExecuteReader(proceduce, param: dynamicParameters, commandType: CommandType.StoredProcedure);
                var voucherId = Guid.Empty;
                while (reader.Read())
                {
                    voucherId = reader.GetGuid(0);
                }
                reader.Close();
                return new
                {
                    voucherId = voucherId
                };
            }
        }

        public int updateAccountVoucher(Guid accountVoucherID, AccountVoucherData data)
        {
            throw new NotImplementedException();
        }
    }
}
