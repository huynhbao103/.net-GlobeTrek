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
    
    public partial class CustomerInfo
    {
        public int id { get; set; }
        public int orderId { get; set; }
        public string fullName { get; set; }
        public string phone { get; set; }
        public string email { get; set; }
    
        public virtual Order Order { get; set; }
    }
}
