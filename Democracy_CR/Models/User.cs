using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Democracy_CR.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required(ErrorMessage = "The field {0} is required")]
        [StringLength(100, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 7)]
        [Display(Name = "Email")]
        [DataType(DataType.EmailAddress)]
        [Index("EmailIndex", IsUnique = true)]
        public string UserName { get; set; }

        [Required(ErrorMessage = "The field {0} is required")]
        [StringLength(50, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 2)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "The field {0} is required")]
        [StringLength(50, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 2)]
        [Display(Name = "Last name")]
        public string LastName { get; set; }

        [Display(Name = "User")]
        public string FullName { get { return string.Format("{0} {1}", this.FirstName, this.LastName); } }

        [Required(ErrorMessage = "The field {0} is required")]
        [StringLength(20, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 7)]
        public string Phone { get; set; }

        [Required(ErrorMessage = "The field {0} is required")]
        [StringLength(100, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 10)]
        public string Address { get; set; }

        public string Grade { get; set; }

        public string Group { get; set; }

        [StringLength(200, ErrorMessage = "The field {0} can contain minimun {2} and maximun {1} characters", MinimumLength = 0)]
        [DataType(DataType.ImageUrl)]
        public string Photo { get; set; }

        public virtual ICollection<GroupMember> GroupMembers { get; set; }

        public virtual ICollection<Candidate> Candidates { get; set; }

        public virtual ICollection<VotingDetail> VotingDetails { get; set; }
    }
}