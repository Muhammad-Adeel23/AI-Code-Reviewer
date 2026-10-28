using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Components.Common
{
    public class Enumerator
    {
    }
    public enum DatabaseOperation
    {
        [Description("SELECT")]
        SELECT = 0,
        [Description("INSERT")]
        INSERT = 1,
        [Description("UPDATE")]
        UPDATE = 2,
        [Description("DELETE")]
        DELETE = 3,
        [Description("GET")]
        GET = 4,
        [Description("GetDataSet")]
        GetDataSet = 5,
        [Description("GetDataTable")]
        GetDataTable = 6,
        [Description("BulkInsert")]
        BulkInsert = 7,
        [Description("BulkUpdate")]
        BulkUpdate = 8,
        [Description("SQLBlock")]
        SQLBlock = 9,
    }
    public enum EmailNotificationEnum
    {
        [Description("Pending")]
        Pending = 200001,
        [Description("Inprogress")]
        Inprogress = 200002,
        [Description("Completed")]
        Completed = 200003,
        [Description("Failed")]
        Failed = 200004

    }

    public enum EmailTemplateEnum
    {
        [Description("Verficatation Email")]
        VerficatationEmail = 100001,

        [Description(" Welcome Email")]
        WelcomeEmail = 100002,

        [Description("  Reset Password")]
        ResetPassword = 100003,

        [Description("Password Changed")]
        PasswordChanged = 100004,

        [Description("Member Invitation")]
        InviteMember = 100005,


        [Description("Review Complete")]
        ReviewComplete = 100005

    }
}
