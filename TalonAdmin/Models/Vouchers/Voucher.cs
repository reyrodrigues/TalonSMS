using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Vouchers
{
    public class Voucher : TenantEntity
    {
        public virtual int CategoryId { get; set; }
        public virtual int DistributionId { get; set; }

        [Index(IsUnique = false), StringLength(30)]
        public virtual string VoucherCode { get; set; }

        /// <summary>
        /// This code is used for backwards compatibility.
        /// This is used to link a paper voucher printed out outside of the system and it is used to correlate with a real voucher in the POSs
        /// </summary>
        [Index(IsUnique = false), StringLength(30)]
        public virtual string SequentialCode { get; set; }


        /// <summary>
        /// 1. Unclaimed
        /// 2. Partially Claimed
        /// 3. Claimed
        /// 4. Cancelled
        /// </summary>
        public virtual int? Status { get; set; }
        public virtual decimal? Value { get; set; }

        public virtual DistributionVoucherCategory Category { get; set; }
        public virtual Distribution Distribution { get; set; }
        public virtual ICollection<VoucherTransactionRecord> TransactionRecords { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual VoucherTransactionRecord IssuingTransactionRecord
        {
            get
            {
                return TransactionRecords.Where(t => t.Type == 1).OrderBy(t => t.LastModifiedOn).FirstOrDefault();
            }
        }

        [JsonIgnore, XmlIgnore]
        public virtual VoucherTransactionRecord LatestCreditTransactionRecords
        {
            get
            {
                return TransactionRecords.Where(t => t.Type == 2).OrderByDescending(t => t.LastModifiedOn).FirstOrDefault();
            }
        }

        [JsonIgnore, XmlIgnore]
        public virtual decimal RemainingValue
        {
            get
            {
                return (Value ?? 0) - TransactionRecords.Where(t => t.Type == 2).Select(t => t.Value ?? 0).Sum();
            }
        }

        public string StatusString
        {
            get
            {
                switch (Status)
                {
                    case 1:
                        return "Unclaimed";
                    case 2:
                        return "Partially Claimed";
                    case 3:
                        return "Claimed";
                    case 4:
                        return "Cancelled";
                    default:
                        return "";
                }
            }
        }
    }
}