using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;

namespace TalonAdmin.Models.Vouchers
{
    public class Vendor : CountryEntity
    {
        public virtual string Name { get; set; }
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual string MobileNumber { get; set; }
        /// <summary>See <see cref="Beneficiary.PersonalIdentifier"/>.</summary>
        [Column("NationalId")]
        public virtual string PersonalIdentifier { get; set; }

        public virtual string UserName { get; set; }
        public virtual string Password { get; set; }

        public virtual int? LocationId { get; set; }
        public virtual int? TypeId { get; set; }
        public virtual int? ParentRecordId { get; set; }

        public virtual string AuthorizationToken { get; set; }
        public virtual string GPSCoordinates { get; set; }

        public virtual VendorType Type { get; set; }
        public virtual Location Location { get; set; }

        public virtual Vendor ParentRecord { get; set; }
        public virtual ICollection<VendorSalesPerson> SalesPersons { get; set;  }


        [NotMapped]
        public JToken AdditionalDataObject
        {
            get
            {
                if (AdditionalData == null)
                {
                    return new JObject();
                }
                return JToken.FromObject(AdditionalData.ToDictionary(k => k.Key, v => v.Value));
            }
        }

        public virtual ICollection<VendorAdditionalData> AdditionalData { get; set; }

        public void SetPassword(string plainText)
        {
            var salt = RandomNumberGenerator.GetBytes(16);

            var iterations = 1000;
            // SHA1 is pinned deliberately: it is what the parameterless EF6 overload used, and
            // changing it would invalidate every vendor password already in the database.
            var hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, iterations, HashAlgorithmName.SHA1, 16);
            this.Password = String.Format("{0}:{1}:{2}", Convert.ToBase64String(salt), Convert.ToBase64String(hash), iterations);
        }

        public bool ValidatePassword(string plainText)
        {
            if (String.IsNullOrEmpty(Password) || String.IsNullOrEmpty(plainText))
                return false;
            if (Password.Where(c=> c == ':').Count() != 2)
                return false;

            var saltAndHash = Password.Split(':');
            var salt = Convert.FromBase64String(saltAndHash[0]);
            var iterations = Convert.ToInt32(saltAndHash[2]);
            var hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, iterations, HashAlgorithmName.SHA1, 16);

            var rightSide = String.Format("{0}:{1}:{2}", Convert.ToBase64String(salt), Convert.ToBase64String(hash), iterations);

            return this.Password == rightSide;
        }
    }
}