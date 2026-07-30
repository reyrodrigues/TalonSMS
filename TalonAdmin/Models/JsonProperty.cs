using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace TalonAdmin.Models
{
    [ComplexType()]
    public class JsonProperty : JObject
    {
    }
}