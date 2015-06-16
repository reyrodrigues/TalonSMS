using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace TalonAdmin.Models.BindingModels
{
    public class UpdateProfileBindingModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }

        public string UserName { get; set; }

        public int OrganizationId { get; set; }

        public int[] CountryIds { get; set; }

        public string[] RoleIds { get; set; }

        public string Role { get; set; }
    }
}
