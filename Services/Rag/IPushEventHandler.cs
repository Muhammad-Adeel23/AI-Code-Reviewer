using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public interface IPushEventHandler
    {
        Task HandleAsync(string payload);
    }
}
