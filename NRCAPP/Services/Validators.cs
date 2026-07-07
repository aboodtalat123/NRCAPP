using FluentValidation;
using NRCAPP.Api;

namespace NRCAPP.Services;

public sealed class OrganizationRegistrationValidator : AbstractValidator<OrganizationRegistrationRequest>
{
    public OrganizationRegistrationValidator()
    {
        RuleFor(x => x.LicenseId).NotEmpty().WithMessage("رقم الترخيص مطلوب.");
        RuleFor(x => x.NgoName).NotEmpty().WithMessage("اسم المؤسسة مطلوب.");
        RuleFor(x => x.AuthorizedPerson).NotEmpty().WithMessage("اسم الشخص المخول مطلوب.");
        RuleFor(x => x.Passcode).NotEmpty().MinimumLength(6).WithMessage("رمز الدخول يجب أن يكون 6 خانات على الأقل.");
    }
}

public sealed class OrganizationLoginValidator : AbstractValidator<OrganizationLoginRequest>
{
    public OrganizationLoginValidator()
    {
        RuleFor(x => x.LicenseId).NotEmpty().WithMessage("رقم الترخيص مطلوب.");
        RuleFor(x => x.Passcode).NotEmpty().WithMessage("رمز الدخول مطلوب.");
    }
}

public sealed class CitizenRegistrationValidator : AbstractValidator<CitizenRegistrationRequest>
{
    public CitizenRegistrationValidator()
    {
        RuleFor(x => x.NationalId).NotEmpty().WithMessage("رقم الهوية مطلوب.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("الاسم مطلوب.");
        RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("رقم الهاتف مطلوب.");
        RuleFor(x => x.CurrentSector).NotEmpty().WithMessage("القطاع مطلوب.");
        RuleFor(x => x.FamilyMembersCount).GreaterThan(0).WithMessage("عدد أفراد الأسرة يجب أن يكون أكبر من صفر.");
    }
}

public sealed class DistributionPlanValidator : AbstractValidator<DistributionPlanRequest>
{
    public DistributionPlanValidator()
    {
        RuleFor(x => x.TargetSector).NotEmpty().WithMessage("القطاع المستهدف مطلوب.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية يجب أن تكون أكبر من صفر.");
        RuleFor(x => x.MaxBeneficiaryCapacity).GreaterThan(0).WithMessage("السعة القصوى يجب أن تكون أكبر من صفر.");
        RuleFor(x => x.OrganizationId).GreaterThan(0).WithMessage("معرّف المؤسسة مطلوب.");
    }
}
