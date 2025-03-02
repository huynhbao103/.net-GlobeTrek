//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GlobeTrek.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Order
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Order()
        {
            this.CustomerInfoes = new HashSet<CustomerInfo>();
        }
    
        public int id { get; set; }
        public Nullable<System.DateTime> orderDate { get; set; }
        public Nullable<System.DateTime> createdAt { get; set; }
        public int userId { get; set; }
        public int tourId { get; set; }
        public decimal adultPrice { get; set; }
        public decimal childPrice { get; set; }
        public int adultCount { get; set; }
        public int childCount { get; set; }
        public decimal totalValue { get; set; }
        public System.DateTime bookingDate { get; set; }
        public string status { get; set; }
        public string paymentMethod { get; set; }
        public Nullable<System.DateTime> updatedAt { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CustomerInfo> CustomerInfoes { get; set; }
        public virtual Tour Tour { get; set; }
        public virtual User User { get; set; }
    }
}
