namespace ShaunERP.Core.Hrms;

public class Department : BaseEntity
{
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class Employee : BaseEntity
{
    public int? CompanyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public string? JobTitle { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}

public class LeaveRequest : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string LeaveType { get; set; } = "Vacation";
    public string? Reason { get; set; }
    public bool Approved { get; set; }
}
