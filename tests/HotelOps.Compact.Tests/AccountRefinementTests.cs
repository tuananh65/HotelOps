using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Compact.Contracts;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class AccountRefinementTests
{
    [Theory]
    [InlineData("Abcd123!",true)]
    [InlineData("Abc123!",false)]
    [InlineData("abcd123!",false)]
    [InlineData("ABCD123!",false)]
    [InlineData("Abcdefg!",false)]
    [InlineData("Abcd1234",false)]
    [InlineData("Abcd123 ",false)]
    [InlineData("Abcd123💡",true)]
    public void EnforcesEveryPasswordCriterion(string password,bool expected)
        =>Assert.Equal(expected,AccountPasswordPolicy.IsValid(password));

    [Fact] public void PasswordLengthUsesTheSameUtf16LimitAsTheBrowser()
    {
        Assert.True(AccountPasswordPolicy.IsValid("Aa1!"+new string('a',124)));
        Assert.False(AccountPasswordPolicy.IsValid("Aa1!"+new string('a',125)));
        Assert.False(AccountPasswordPolicy.IsValid(null));
    }

    [Fact] public void CreationRequiresBranchAndValidContactFieldsButNoEmployeeCode()
    {
        var valid=new CreateAccountRequest("user","Nguyễn Văn An","Abcd123!",1);
        Assert.True(Valid(valid));
        Assert.False(Valid(valid with{BranchId=0}));
        Assert.False(Valid(valid with{DepartmentId=-1}));
        Assert.False(Valid(valid with{Email="invalid"}));
        Assert.False(Valid(valid with{Phone="not a phone"}));
        Assert.True(Valid(new ChangeAccountPasswordRequest("old","Abcd123!")));
        Assert.False(Valid(new ChangeAccountPasswordRequest("old","Abc123!")));
    }

    // MVC validates record constructor parameters, not synthesized properties.
    private static bool Valid(object value)=>value.GetType().GetConstructors().Single().GetParameters().All(parameter=>
        Validator.TryValidateValue(value.GetType().GetProperty(parameter.Name!)!.GetValue(value),
            new ValidationContext(value){MemberName=parameter.Name},[],
            parameter.GetCustomAttributes(typeof(ValidationAttribute),true).Cast<ValidationAttribute>()));
}
