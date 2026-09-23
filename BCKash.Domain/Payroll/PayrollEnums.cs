namespace BCKash.Domain.Payroll;

/// <summary>Legacy values of `payroll.recur_type`.</summary>
public enum PayrollRecurType
{
    Days,
    Weeks,
    Months,
    Years,
}

/// <summary>Legacy values of `payroll_meta.position`.</summary>
public enum PayrollMetaPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Legacy values of `payroll_template_meta.position` (adds `none`, unlike <see cref="PayrollMetaPosition"/>).</summary>
public enum PayrollTemplateMetaPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    None,
}

/// <summary>Legacy values of `payroll_template_meta.type`.</summary>
public enum PayrollTemplateMetaType
{
    Addition,
    Deduction,
}

/// <summary>Legacy values of `payroll_template_meta.tax_on`.</summary>
public enum PayrollTemplateMetaTaxOn
{
    Net,
    Gross,
}
