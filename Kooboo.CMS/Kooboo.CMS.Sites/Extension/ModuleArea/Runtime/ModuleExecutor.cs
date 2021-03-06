﻿#region License
// 
// Copyright (c) 2013, Kooboo team
// 
// Licensed under the BSD License
// See the file LICENSE.txt for details.
// 
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web;
using Kooboo.CMS.Sites.Controllers;
using Kooboo.CMS.Sites.View;
using System.Web.Routing;
using Kooboo.Web;
using Kooboo.CMS.Sites.Models;

namespace Kooboo.CMS.Sites.Extension.ModuleArea.Runtime
{
    public static class ModuleExecutor
    {
        static ModuleControllerActionInvoker actionInvoker = new ModuleControllerActionInvoker();
        public static ModuleActionInvokedContext InvokeAction(ControllerContext controllerContext, Site site, string moduleUrl, ModulePosition modulePosition)
        {
            try
            {
                HttpContext context = HttpContext.Current;

                var positionId = modulePosition.PagePositionId;
                var moduleName = modulePosition.ModuleName;

                ModuleContext moduleContext = ModuleContext.Create(moduleName, site, modulePosition);
                ModuleContext.Current = moduleContext;

                if (string.IsNullOrEmpty(moduleUrl))
                {
                    var entry = modulePosition.Entry;
                    if (entry == null)
                    {
                        var moduleSetting = moduleContext.GetModuleSettings();
                        if (moduleSetting != null)
                        {
                            entry = moduleSetting.Entry;
                        }
                    }
                    if (entry != null)
                    {
                        moduleUrl = GetEntryUrl(context, moduleContext, entry);
                        if (!string.IsNullOrEmpty(moduleUrl) && !moduleUrl.StartsWith("~"))
                        {
                            moduleUrl = "~" + moduleUrl;
                        }
                    }
                }
                if (string.IsNullOrEmpty(moduleUrl))
                {
                    moduleUrl = "~/";
                }
                else if (moduleUrl[0] != '~')
                {
                    moduleUrl = "~/" + moduleUrl.TrimStart('/');
                }

                var httpContext = new ModuleHttpContext(context
                  , new ModuleHttpRequest(context.Request, moduleUrl, moduleContext, controllerContext), new ModuleHttpResponse(context.Response, moduleContext), moduleContext);

                var routeData = moduleContext.FrontEndContext.RouteTable.GetRouteData(httpContext);

                var requestContext = new ModuleRequestContext(httpContext, routeData, moduleContext) { PageControllerContext = controllerContext };

                string controllerName = requestContext.RouteData.GetRequiredString("controller");
                string actionName = requestContext.RouteData.GetRequiredString("action");
                var controller = (Controller)ControllerBuilder.Current.GetControllerFactory().CreateController(requestContext, controllerName);
                if (controller == null)
                {
                    throw new Exception(string.Format("The module '{0}' controller for path '{1}' does not found or does not implement IController.", moduleName, moduleUrl));
                }
                //if (!(controller is ModuleControllerBase))
                //{
                //    throw new Exception(string.Format("The controller type '{0}' must be inherited from ModuleControllerBase.", controller.GetType().FullName));
                //}
                //ModuleControllerBase moduleController = (ModuleControllerBase)controller;

                //ControllerContext moduleControllerContext = new ControllerContext(requestContext, moduleController);

                InitializeController(controller, requestContext);

                var result = actionInvoker.InvokeActionWithoutExecuteResult(modulePosition, controller.ControllerContext, actionName);
                if (result == null)
                {
                    HandleUnknownAction(controller, actionName);
                }
                return result;
            }
            catch
            {
                if (modulePosition.SkipError)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }

        }
        private static void InitializeController(Controller moduleController, ModuleRequestContext moduleRequestContext)
        {
            moduleController.ControllerContext = new ControllerContext(moduleRequestContext, moduleController);

            var pageControllerContext = moduleRequestContext.PageControllerContext;

            var valueProvider = new ValueProviderCollection();
            valueProvider.Add(new ModuleFormValueProvider(moduleController.ControllerContext));
            valueProvider.Add(new ModuleQueryStringValueProvider(moduleController.ControllerContext));
            valueProvider.Add(new RouteDataValueProvider(moduleController.ControllerContext));
            if (Kooboo.CMS.Sites.View.Page_Context.Current.PageRequestContext != null)
            {
                valueProvider.Add(new NameValueCollectionValueProvider(Kooboo.CMS.Sites.View.Page_Context.Current.PageRequestContext.AllQueryString, System.Globalization.CultureInfo.InvariantCulture));
            }
            valueProvider.Add(pageControllerContext.Controller.ValueProvider);

            moduleController.ValueProvider = valueProvider;

            moduleController.TempDataProvider = new ModuleSessionStateTempDataProvider();

            moduleController.Url = new UrlHelper(moduleController.ControllerContext.RequestContext, moduleRequestContext.ModuleContext.FrontEndContext.RouteTable);
        }
        private class GetEntryUrlContextWrapper : HttpContextWrapper
        {
            private class GetEntryUrlRequestWrapper : ModuleHttpRequest
            {
                public GetEntryUrlRequestWrapper(HttpRequest request, string requestUrl)
                    : base(request, requestUrl)
                {

                }
            }
            HttpContext _context;
            string requestUrl;
            public GetEntryUrlContextWrapper(HttpContext context, string requestUrl)
                : base(context)
            {
                _context = context;
                this.requestUrl = requestUrl;
            }
            public override HttpRequestBase Request
            {
                get
                {
                    return new GetEntryUrlRequestWrapper(_context.Request, requestUrl);
                }
            }
        }
        private static string GetEntryUrl(HttpContext context, ModuleContext moduleContext, Entry entry)
        {
            var httpContext = new GetEntryUrlContextWrapper(context, "~/");

            var routeData = moduleContext.FrontEndContext.RouteTable.GetRouteData(httpContext);

            var requestContext = new ModuleRequestContext(httpContext, routeData, moduleContext);

            UrlHelper url = new UrlHelper(requestContext, moduleContext.FrontEndContext.RouteTable);

            return url.Action(entry.Action, entry.Controller, entry.Values);
        }
        private static void HandleUnknownAction(Controller controller, string actionName)
        {
            throw new HttpException(0x194, string.Format(SR.GetString("Controller_UnknownAction"), new object[] { actionName, controller.GetType().FullName }));
        }

        public static ModuleResultExecutedContext ExecuteActionResult(ModuleActionInvokedContext actionInvokedContext)
        {
            try
            {
                //switch the module context for the view render.
                ModuleContext.Current = actionInvokedContext.ControllerContext.GetModuleContext();

                return actionInvoker.ExecuteActionResult(actionInvokedContext);
            }
            catch (Exception e)
            {
                if (actionInvokedContext.ModulePosition.SkipError)
                {
                    return new ModuleResultExecutedContext(actionInvokedContext.ControllerContext, actionInvokedContext.ActionResult, false, e, "");
                }
                else
                {
                    throw;
                }
            }

        }
    }
}
