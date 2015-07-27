using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Vendor : CountryEntity
    {
        public virtual string Name { get; set; }
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual string MobileNumber { get; set; }
        public virtual string NationalId { get; set; }

        public virtual string UserName { get; set; }
        public virtual string Password { get; set; }

        public virtual int? LocationId { get; set; }
        public virtual int? TypeId { get; set; }
        public virtual int? ParentRecordId { get; set; }

        public virtual string GPSCoordinates { get; set; }

        public virtual VendorType Type { get; set; }
        public virtual Location Location { get; set; }

        public virtual Vendor ParentRecord { get; set; }

        public void SetPassword(string plainText)
        {
            RNGCryptoServiceProvider generator = new RNGCryptoServiceProvider();
            var salt = new byte[16];
            generator.GetBytes(salt);

            var iterations = 1000;
            var passwordHasher = new Rfc2898DeriveBytes(plainText, salt, iterations);
            this.Password = String.Format("{0}:{1}:{2}", Convert.ToBase64String(salt), Convert.ToBase64String(passwordHasher.GetBytes(16)), iterations);
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
            var passwordHasher = new Rfc2898DeriveBytes(plainText, salt, iterations);
            
            var rightSide = String.Format("{0}:{1}:{2}", Convert.ToBase64String(salt), Convert.ToBase64String(passwordHasher.GetBytes(16)), iterations);

            return this.Password == rightSide;
        }
    }
}