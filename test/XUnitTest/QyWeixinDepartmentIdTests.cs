using System.ComponentModel;
using NewLife.Cube.QyWeixin;
using Xunit;

namespace XUnitTest;

public class QyWeixinDepartmentIdTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("100005001", 100005001)]
    [InlineData(" 42 ", 42)]
    [InlineData("2147483647", Int32.MaxValue)]
    [InlineData("2147483648", 2147483648L)]
    [InlineData("4031251834", 4031251834L)]
    [InlineData("4294967295", 4294967295L)]
    [DisplayName("部门Id_合法的32位无符号正整数可解析")]
    public void TryParse_Valid(String text, Int64 expected)
    {
        Assert.True(QyWeixinDepartmentId.TryParse(text, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1,000")]
    [InlineData("4294967296")]
    [InlineData("abc")]
    [DisplayName("部门Id_非法或超出32位无符号范围拒绝解析")]
    public void TryParse_Invalid(String text)
    {
        Assert.False(QyWeixinDepartmentId.TryParse(text, out var id));
        Assert.Equal(0, id);
    }

    [Fact]
    [DisplayName("部门Id_解析失败抛出而不是静默归零")]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QyWeixinDepartmentId.Parse("4294967296"));
        Assert.Throws<ArgumentOutOfRangeException>(() => QyWeixinDepartmentId.Parse(""));
    }
}
