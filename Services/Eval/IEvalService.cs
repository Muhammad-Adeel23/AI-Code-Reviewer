using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Eval
{
    public interface IEvalService
    {
        Task<object> RunAsync(int organizationId, EvalRequest request, CancellationToken ct);
    }
}
