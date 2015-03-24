using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TalonAdmin.Models.BindingModels
{
    public class RemoveUserFromRoleBindingModel
    {
        public string Email { get; set; }

        public string RoleName { get; set; }
    }
}
