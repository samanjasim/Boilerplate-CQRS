using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Starter.Module.AI;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Controllers;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiSettingsControllerShapeTests
{
    [Fact]
    public void Controller_Has_Expected_Route()
    {
        var routes = typeof(AiSettingsController)
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(r => r.Template)
            .ToArray();

        routes.Should().Contain("api/v{version:apiVersion}/ai/settings");
    }

    [Fact]
    public void Write_Actions_Require_AiManageSettings()
    {
        var writeMethods = typeof(AiSettingsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is null)
            .ToArray();

        writeMethods.Should().NotBeEmpty();
        writeMethods.All(HasManageSettingsPolicy).Should().BeTrue();
    }

    [Fact]
    public void Provider_Credential_Endpoints_Exist()
    {
        AssertEndpoint(nameof(AiSettingsController.GetProviderCredentials), typeof(HttpGetAttribute), "provider-credentials");
        AssertEndpoint(nameof(AiSettingsController.CreateProviderCredential), typeof(HttpPostAttribute), "provider-credentials");
        AssertEndpoint(nameof(AiSettingsController.RotateProviderCredential), typeof(HttpPostAttribute), "provider-credentials/{id:guid}/rotate");
        AssertEndpoint(nameof(AiSettingsController.RevokeProviderCredential), typeof(HttpPostAttribute), "provider-credentials/{id:guid}/revoke");
        AssertEndpoint(nameof(AiSettingsController.TestProviderCredential), typeof(HttpPostAttribute), "provider-credentials/{id:guid}/test");
    }

    [Fact]
    public void Model_Default_Endpoints_Exist()
    {
        AssertEndpoint(nameof(AiSettingsController.GetModelDefaults), typeof(HttpGetAttribute), "model-defaults");
        AssertEndpoint(nameof(AiSettingsController.UpsertModelDefault), typeof(HttpPutAttribute), "model-defaults/{agentClass}");
    }

    [Fact]
    public void Widget_Endpoints_Exist()
    {
        AssertEndpoint(nameof(AiSettingsController.GetWidgets), typeof(HttpGetAttribute), "widgets");
        AssertEndpoint(nameof(AiSettingsController.CreateWidget), typeof(HttpPostAttribute), "widgets");
        AssertEndpoint(nameof(AiSettingsController.UpdateWidget), typeof(HttpPutAttribute), "widgets/{id:guid}");
        AssertEndpoint(nameof(AiSettingsController.CreateWidgetCredential), typeof(HttpPostAttribute), "widgets/{id:guid}/credentials");
        AssertEndpoint(nameof(AiSettingsController.RotateWidgetCredential), typeof(HttpPostAttribute), "widgets/{id:guid}/credentials/{credentialId:guid}/rotate");
        AssertEndpoint(nameof(AiSettingsController.RevokeWidgetCredential), typeof(HttpPostAttribute), "widgets/{id:guid}/credentials/{credentialId:guid}/revoke");
    }

    [Fact]
    public void Admin_Default_Permissions_Include_AiManageSettings()
    {
        var module = new AIModule();
        var admin = module.GetDefaultRolePermissions().Single(r => r.Role == "Admin");

        admin.Permissions.Should().Contain(AiPermissions.ManageSettings);
    }

    private static bool HasManageSettingsPolicy(MethodInfo method)
    {
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        return authorize?.Policy == AiPermissions.ManageSettings;
    }

    private static void AssertEndpoint(string methodName, Type httpAttributeType, string template)
    {
        var method = typeof(AiSettingsController).GetMethod(methodName);
        method.Should().NotBeNull();

        var httpAttribute = method!.GetCustomAttributes(httpAttributeType, inherit: false)
            .Cast<HttpMethodAttribute>()
            .SingleOrDefault();

        httpAttribute.Should().NotBeNull();
        httpAttribute!.Template.Should().Be(template);
    }
}
