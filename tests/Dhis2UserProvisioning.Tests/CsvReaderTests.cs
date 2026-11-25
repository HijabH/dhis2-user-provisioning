using Dhis2UserProvisioning.Csv;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class CsvReaderTests
{
    private const string Header = "firstName,surname,username,email,phoneNumber,userRoles,orgUnits,dataViewOrgUnits,userGroups";

    [Fact]
    public void Reads_a_simple_row()
    {
        var csv = Header + "\nAmina,Khan,akhan,amina@example.org,+92 300 1234567,Data entry clerk,OU_001,,TB team";

        var requests = UserRequestCsvReader.Read(csv);

        var r = Assert.Single(requests);
        Assert.Equal(2, r.RowNumber);
        Assert.Equal("Amina", r.FirstName);
        Assert.Equal("akhan", r.Username);
        Assert.Equal(new[] { "Data entry clerk" }, r.UserRoles);
        Assert.Equal(new[] { "OU_001" }, r.OrgUnits);
        Assert.Empty(r.DataViewOrgUnits);
        Assert.Equal(new[] { "TB team" }, r.UserGroups);
    }

    [Fact]
    public void Splits_semicolon_lists_trims_and_removes_duplicates()
    {
        var csv = Header + "\nA,B,user1,a@b.org,, Role one ; Role two;role ONE ,OU_1;OU_2,,";

        var r = Assert.Single(UserRequestCsvReader.Read(csv));

        Assert.Equal(new[] { "Role one", "Role two" }, r.UserRoles);
        Assert.Equal(new[] { "OU_1", "OU_2" }, r.OrgUnits);
    }

    [Fact]
    public void Handles_quoted_fields_with_commas_and_escaped_quotes()
    {
        var csv = Header + "\n\"Ali, Jr.\",\"O\"\"Brien\",aobrien,a@b.org,,Clerk,OU_1,,";

        var r = Assert.Single(UserRequestCsvReader.Read(csv));

        Assert.Equal("Ali, Jr.", r.FirstName);
        Assert.Equal("O\"Brien", r.Surname);
    }

    [Fact]
    public void Ignores_blank_lines_and_windows_line_endings()
    {
        var csv = Header + "\r\nA,B,user1,a@b.org,,Clerk,OU_1,,\r\n\r\nC,D,user2,c@d.org,,Clerk,OU_2,,\r\n";

        var requests = UserRequestCsvReader.Read(csv);

        Assert.Equal(2, requests.Count);
        Assert.Equal("user2", requests[1].Username);
    }

    [Fact]
    public void Strips_byte_order_mark_from_header()
    {
        var csv = "﻿" + Header + "\nA,B,user1,a@b.org,,Clerk,OU_1,,";

        var r = Assert.Single(UserRequestCsvReader.Read(csv));

        Assert.Equal("A", r.FirstName);
    }

    [Fact]
    public void Optional_columns_can_be_left_out()
    {
        var csv = "firstName,surname,username,email,userRoles,orgUnits\nA,B,user1,a@b.org,Clerk,OU_1";

        var r = Assert.Single(UserRequestCsvReader.Read(csv));

        Assert.Equal("", r.PhoneNumber);
        Assert.Empty(r.UserGroups);
    }

    [Fact]
    public void Throws_when_required_columns_are_missing()
    {
        var csv = "firstName,surname,username\nA,B,user1";

        var ex = Assert.Throws<FormatException>(() => UserRequestCsvReader.Read(csv));

        Assert.Contains("email", ex.Message);
        Assert.Contains("orgUnits", ex.Message);
    }

    [Fact]
    public void Throws_on_unclosed_quote()
    {
        var csv = Header + "\n\"A,B,user1,a@b.org,,Clerk,OU_1,,";

        Assert.Throws<FormatException>(() => UserRequestCsvReader.Read(csv));
    }

    [Fact]
    public void Throws_on_empty_file()
    {
        Assert.Throws<FormatException>(() => UserRequestCsvReader.Read(""));
    }
}
