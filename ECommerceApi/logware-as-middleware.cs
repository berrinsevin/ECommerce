using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text.Json;
using Ccp.Saga.Transaction;
using System.Threading.Tasks;
using Intertech.Orion.Service;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Intertech.CallCenterPlusNuget.Dto;
using Microsoft.Extensions.Configuration;
using Intertech.CallCenterPlusNuget.Helper;
using Intertech.CallCenterPlusNuget.Crypto;
using Microsoft.AspNetCore.Mvc.Controllers;
using Intertech.CallCenterPlusNuget.Service;
using Intertech.CallCenterPlusNuget.Validator;
using Intertech.Aether.Abstraction.Exceptions;
using Intertech.CallCenterPlusNuget.Annotation;
using Microsoft.Extensions.DependencyInjection;
using Intertech.CallCenterPlus.RequestResponse;
using Intertech.CallCenterPlusNuget.MultiLanguage;
using Intertech.CallCenterPlusNuget.CommonContext.App;
using Intertech.CallCenterPlusNuget.Utils;

namespace Intertech.CallCenterPlusNuget.Middleware
{
    /// <summary>
    /// Logging middleware
    /// </summary>
    public class Logware
    {
        private readonly ILogger<Logware> logwareLogger;
        private readonly RequestDelegate next;
        /// <summary>
        /// Logging middleware
        /// </summary>
        /// <param name="_next"></param>
        /// <param name="_logwareLogger"></param>
        public Logware(RequestDelegate _next, ILogger<Logware> _logwareLogger)
        {
            next = _next;
            logwareLogger = _logwareLogger;
        }

        /// <summary>
        /// Request delegator
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cryptolog"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, ICryptolog cryptolog, IConfiguration configuration)
        {
            Guard.NotNull(context, nameof(HttpContext));

            var endpoint = context.GetEndpoint();
            var canByPassContextGeneration = CanByPassContextGeneration(endpoint);

            CcpLog ccpLog = new CcpLog
            {
                RequestDate = DateTime.Now,
                MethodName = context.Request.Path
            };

            IRequestResponseLogger logger = null;
            var originalBodyStream = context.Response.Body;

            try
            {
                ccpLog.ApplicationName = configuration?["Aether:ServiceName"];

                if (!canByPassContextGeneration)
                {
                    PrepareContext(context, cryptolog, configuration?["ENVIRONMENT"]);
                }
                _ = CcpContext.Current; // get for once to fill props 

                logger = context.RequestServices.GetService<IRequestResponseLogger>();
                bool controlContextRequest = context.Request.Path.StartsWithSegments("/swagger") || context.Request.Path.StartsWithSegments("/health") || context.Request.Method == "OPTIONS";
                if (controlContextRequest || endpoint == null || logger == null || IsHealthCheckEndpoint(context))
                {
                    await next(context);
                    return;
                }

                ccpLog.RequestMessage = await ReadBodyFromRequestAsync(context.Request);

                using (var responseBody = new MemoryStream())
                {
                    context.Response.Body = responseBody;
                    await next(context);

                    ccpLog.ResponseMessage = await ReadBodyFromResponseAsync(context.Response);

                    await context.Response.Body.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex) when (ex != null)
            {
                ccpLog.IsException = true;
                ccpLog.ResponseMessage = ex.ToString();

                logwareLogger.LogCritical($"Logware Ex: {ex}");

                context.Response.StatusCode = Constants.STATUS_CODE;

                var languageService = context.RequestServices.GetService<ICcpMultiLanguage>();

                var exceptionDto = new CcpExceptionDto
                {
                    IsBusinessFailure = true
                };

                if (languageService != null)
                {
                    ICcpMultiLanguage ccpMultiLanguage = languageService;
                    exceptionDto.ErrorMessage = await ccpMultiLanguage.GetLocalLanguageAsync(ex.Message, "CCP");
                }
                else
                {
                    exceptionDto.ErrorMessage = ex.Message;
                }

                context.Response.Body = originalBodyStream;

                await context.Response.WriteAsJsonAsync(exceptionDto, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
            }

            ccpLog.ResponseDate = DateTime.Now;
            logger?.TryCreateLogItem(ccpLog);
            await LoggerInvokeAsync(canByPassContextGeneration, context, endpoint, ccpLog);
        }

        private async Task LoggerInvokeAsync(bool canByPassContextGeneration, HttpContext context, Endpoint endpoint, CcpLog ccpLog)
        {
            try
            {
                if (!canByPassContextGeneration && CcpContext.Current.AuthToken?.DecryptedCall != null) // contextli bir işlemse transaction log atılmalı.
                {
                    var hasJwtKey = context.Request.Headers?.ContainsKey("Authorization");
                    if (!hasJwtKey.HasValue || !hasJwtKey.Value)
                    {
                        return;
                    }

                    var controllerActionDescriptor = endpoint?.Metadata?.GetMetadata<ControllerActionDescriptor>();

                    string agentUserCode = CcpContext.Current.AuthToken?.Agent?.UserCode;
                    if (string.IsNullOrEmpty(agentUserCode))
                    {
                        throw new BusinessException("TransactionSagaUserCodeEmpty");
                    }

                    CcpContext.Current.UserCode = agentUserCode;

                    LogTransactionSagaRequest request = new()
                    {
                        TransactionLogRequest = new TransactionLogRequest
                        {
                            Message = ccpLog?.RequestMessage,
                            UpdatingChannelCode = CcpContext.Current.AuthToken.DecryptedCall.Channel,
                            CallId = CcpContext.Current.AuthToken.DecryptedCall.CallId,
                            CustomerNumber = CcpContext.Current.AuthToken.ExecutingCustomerNumber,
                            RelatedCustomerNumber = CcpContext.Current.AuthToken.DecryptedCall.RelatedCustomerNo,
                            MethodName = controllerActionDescriptor?.ActionName,
                            TellerName = agentUserCode,
                            AgentDomain = agentUserCode,
                            UpdatingUserCode = agentUserCode,
                            BranchCode = CcpContext.Current.User?.BranchCode ?? -1 // ??
                        }
                    };

                    var sagaInvoker = (IServiceInstance)context.RequestServices.GetService(typeof(IServiceInstance));
                    await sagaInvoker?.SendAsync(request);
                }
            }
            catch (Exception ex) when (ex != null)
            {
                logwareLogger.LogCritical($"TransactionalSagaSendError {ex}");
            }
        }

        private static async Task<string> ReadBodyFromResponseAsync(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            return text;
        }

        private static async Task<string> ReadBodyFromRequestAsync(HttpRequest request)
        {
            request.EnableBuffering();

            using var streamReader = new StreamReader(request.Body, leaveOpen: true);
            var requestBody = await streamReader.ReadToEndAsync();

            request.Body.Position = 0;
            return requestBody;
        }

        private void PrepareContext(HttpContext context, ICryptolog cryptolog, string environment)
        {
            if (context.Request.Method == "OPTIONS") // preflight requests doesn't have context...
            {
                return;
            }

            var hasContext = context.Request.Headers.ContainsKey("Ccpcontext");

            if (!hasContext)
            {
                if (context.Request.Headers.TryGetValue("Referer", out var referer) && referer.Any(x => x.Contains("/swagger")))
                {
                    return;
                }

                throw new BusinessException("ContextIsEmpty");
            }

            try
            {
                var deserializedContext = context.Request.Headers["Ccpcontext"];
                CcpContext.Current = JsonConvert.DeserializeObject<CcpContext>(deserializedContext);

                FlattenContext(cryptolog, environment);
            }
#pragma warning disable
            catch (System.Exception ex)
            {
                string message = $"PrepareContext exception {ex}";
                logwareLogger.LogCritical(message);

                if (ex is BusinessException)
                {
                    throw;
                }
            }
#pragma warning restore
        }

        private static void FlattenContext(ICryptolog cryptolog, string environment)
        {
            var tokenDecrypted = cryptolog.Decrypt(CcpContext.Current.SessionToken);
            CcpContext.Current.AuthToken = JsonConvert.DeserializeObject<CcpAuthToken>(tokenDecrypted);

            CcpContext.Current.AuthToken.ValidateToken(environment); // token içeriğini valide ediyor
        }

        private static bool CanByPassContextGeneration(Endpoint endpoint)
        {
            if (endpoint == null)
            {
                return true;
            }

            var isMethodWhitelisted = endpoint.Metadata.GetMetadata<NoContextRequiredAttribute>();
            var isControllerMarkedAsContextRequired = endpoint.Metadata.GetMetadata<CcContextRequiredAttribute>();

            return isControllerMarkedAsContextRequired == null || isMethodWhitelisted != null;
        }


        private static bool IsHealthCheckEndpoint(HttpContext httpContext)
        {
            List<string> healthEndpoints = new List<string> { "/HEALTH/LIVE", "/HEALTH/LIVE/", "/HEALTH/READY", "/HEALTH/READY/" };

            string text = httpContext.Request.Path.Value!.ToUpperInvariant();
            foreach (string healthEndpoint in healthEndpoints)
            {
                if (text.Contains(healthEndpoint))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
