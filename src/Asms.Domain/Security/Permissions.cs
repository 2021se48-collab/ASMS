using System.Reflection;

namespace Asms.Domain.Security;

public static class Permissions
{
    public static class Students
    {
        public const string View = "Students.View";
        public const string Create = "Students.Create";
        public const string Edit = "Students.Edit";
        public const string Delete = "Students.Delete";
    }

    public static class Teachers
    {
        public const string View = "Teachers.View";
        public const string Create = "Teachers.Create";
        public const string Edit = "Teachers.Edit";
        public const string Delete = "Teachers.Delete";
    }

    public static class Attendance
    {
        public const string View = "Attendance.View";
        public const string Record = "Attendance.Record";
        public const string Edit = "Attendance.Edit";
    }

    public static class Exams
    {
        public const string View = "Exams.View";
        public const string EnterMarks = "Exams.EnterMarks";
        public const string Finalize = "Exams.Finalize";
        public const string Approve = "Exams.Approve";
    }

    public static class Fees
    {
        public const string View = "Fees.View";
        public const string Invoice = "Fees.Invoice";
        public const string CollectPayment = "Fees.CollectPayment";
        public const string Reverse = "Fees.Reverse";
    }

    public static class Finance
    {
        public const string ViewReports = "Finance.ViewReports";
        public const string ManageExpenses = "Finance.ManageExpenses";
        public const string ManageSalaries = "Finance.ManageSalaries";
    }

    public static class Library
    {
        public const string View = "Library.View";
        public const string Manage = "Library.Manage";
    }

    public static class Inventory
    {
        public const string View = "Inventory.View";
        public const string Manage = "Inventory.Manage";
    }

    public static class Homework
    {
        public const string View = "Homework.View";
        public const string Manage = "Homework.Manage";
    }

    public static class Notifications
    {
        public const string View = "Notifications.View";
        public const string Send = "Notifications.Send";
    }

    public static class Reports
    {
        public const string View = "Reports.View";
        public const string Export = "Reports.Export";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Manage = "Settings.Manage";
    }

    public static class Users
    {
        public const string View = "Users.View";
        public const string Manage = "Users.Manage";
    }

    public static class Audit
    {
        public const string View = "Audit.View";
    }

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray());
}
