using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class EmailTemplateDTO
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string TemplateBody { get; set; }
    }
}
