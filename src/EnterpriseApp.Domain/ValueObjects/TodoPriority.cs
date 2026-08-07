namespace EnterpriseApp.Domain.ValueObjects;

/// <summary>Enumeration-style value object for todo priority.</summary>
public enum TodoPriority { Low = 1, Medium = 2, High = 3 }

/// <summary>Enumeration-style value object for todo status.</summary>
public enum TodoStatus { Pending = 1, InProgress = 2, Completed = 3, Cancelled = 4 }
