using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Democracy_CR.Models
{
    [NotMapped]
    public class UserSettingsView : User
    {
        [Display(Name = "New Photo")]
        public HttpPostedFileBase NewPhoto { get; set; }
    }
}