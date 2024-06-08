import { useHttpService } from "inter-auth";
import { ApiError, ApiErrorData } from "../data/models/core/api-error";
import { NotificationProvider } from "./notification-provider";
import { Helper } from "../helpers/helper";
import { $CommandBus, CommandTypes } from "./command-bus";
import { RequestContext } from "./request-context";
import { GatewayCodes } from "../data/enums/gateway-codes";

interface ApiRequestContext {
    Method: string;
    Request: any;
    Query: boolean;
    Response?: any;
    Error?: any;
    HideError?: boolean;
}

export interface ApiRequest {
    Method: string;
    Request: any;
    SuccessNotification?: boolean;
    HideError?: boolean;
    HideLoading?: boolean;
}

export const ApiCaller = (controllerName: string, gatewayCode?: string) => {
    const { post, get } = useHttpService();
    const notificationProvider = NotificationProvider();
    const requestContext = RequestContext();
    const commonUrl = process.env["NEXT_PUBLIC_GW_URL"]!;

    const getGateWayUrl = (): string => {
        if (gatewayCode === undefined) {
            return commonUrl;
        } else {
            const dashboardCode = requestContext.GetDashboardCode();
            const pascalDashCode = dashboardCode!.charAt(0).toUpperCase() + dashboardCode!.slice(1).toLowerCase();
            const currentGwCode = GatewayCodes[pascalDashCode as keyof typeof GatewayCodes];
            return commonUrl.replace(currentGwCode, gatewayCode);
        }
    };

    const createApiError = (context: ApiRequestContext): any => {
        const response = context.Error;
        const methodName = context.Method;

        console.error("GOGO:ApiError:", { methodName, request: context.Request, response });
        return new ApiError(response.Message, response);
    };

    const getHttpResponse = async (response: Response) => {
        try {
            return await response.json();
        } catch {
            try {
                return await response.text();
            } catch {
                return null;
            }
        }
    };

    const isHttpNoContent = (response: any) => {
        if (Helper.IsNotValue(response) || !Helper.IsObject(response)) {
            return false;
        }

        if (!(response instanceof Response)) {
            return false;
        }

        // 200 veya 204 gelmemesi gerekiyor. Direk sonuç seti gelmesini bekliyoruz.
        // Bu şekilde gelen durumlarda var. Onlar için direk null dönüyoruz.
        if (response.status === 204 || response.status === 200) {
            return true;
        }

        return false;
    };

    const getOrionException = (response: any): ApiErrorData | null => {
        if (Helper.IsNotValue(response) || !Helper.IsObject(response)) {
            return null;
        }

        if (response.data && response.data.IsBusinessError) {
            response = response.data;
        }

        if ("StatusCode" in response && "IsBusinessError" in response) {
            return {
                Message: response.Message,
                ErrorCode: response.ErrorCode,
                BusinessError: response.IsBusinessError
            };
        }

        // Çoklu dil sorgulamasında alınan hata tipi
        if ("ErrorMessage" in response && "IsBusinessFailure" in response) {
            return {
                Message: response.ErrorMessage,
                BusinessError: response.IsBusinessFailure
            };
        }

        return null;
    };

    const getHttpException = async (response: any): Promise<ApiErrorData | null> => {
        if (Helper.IsNotValue(response) || !Helper.IsObject(response)) {
            return null;
        }

        if (!(response instanceof Response)) {
            return null;
        }

        if (response.status < 400) {
            return null;
        }

        try {
            const jsonResponse = await response.json();
            if (Helper.IsObject(jsonResponse)) {
                return {
                    Message: jsonResponse.ErrorMessage || jsonResponse.title,
                    BusinessError: true
                };
            }
        } catch {
            try {
                const text = await response.text();
                if (Helper.IsNotEmptyValue(text)) {
                    return {
                        Message: text,
                        BusinessError: true
                    };
                }
            } catch {
                console.log("exception occured");
            }
        }

        return {
            Message: response.statusText,
            ErrorCode: response.status.toString(),
            BusinessError: false
        };
    };

    const tryGetError = async (response: any) => {
        let exception = await getHttpException(response);
        if (exception === null) {
            exception = getOrionException(response);
        }

        return exception;
    };

    const handleResponse = async (context: ApiRequestContext) => {
        const error = await tryGetError(context.Response);
        if (Helper.IsNotValue(error)) {
            return context.Response;
        }

        context.Error = error;
        return createApiError(context);
    };

    const handleException = async (context: ApiRequestContext) => {
        const error = await tryGetError(context.Error);
        if (error === null) {
            // hata ayıklanamadı
            return context.Error;
        }

        context.Error = error;
        return createApiError(context);
    };

    const GetAsync = async <TResponse>(methodName: string, query: string) => {
        const context: ApiRequestContext = {
            Method: methodName,
            Query: true,
            Request: query
        };

        try {
            if (gatewayCode !== undefined) {
                context.Response = await get(getGateWayUrl() + controllerName + methodName + query);
            }
            context.Response = await get(getGateWayUrl() + controllerName + methodName + query);
            return handleResponse(context) as TResponse;
        } catch (e: any) {
            context.Error = e;
            return handleException(context);
        }
    };

    const createCallCenterHeaders = (): RequestInit => {
        const token = requestContext.GetToken() ?? null;
        const ccpContext = {
            SessionToken: token
        };

        const headers: HeadersInit = {
            Ccpcontext: JSON.stringify(ccpContext)
        };

        const context: RequestInit = {
            headers: headers
        };

        return context;
    };

    const PostAsync = async <TResponse>(context: ApiRequestContext) => {
        let target = "";
        try {
            const headers: RequestInit = createCallCenterHeaders();
            target = getGateWayUrl() + controllerName + context.Method;
            context.Response = await post(target, context.Request, headers);
            if (isHttpNoContent(context.Response)) {
                return getHttpResponse(context.Response);
            }

            return handleResponse(context) as TResponse;
        } catch (e: any) {
            context.Error = e;
            console.error(`GOGO:APIERROR:${target}`, context);
            return handleException(context);
        }
    };

    const QueryAsync = <TResponse>(methodName: string, request: any) => {
        const context: ApiRequestContext = {
            Method: methodName,
            Query: true,
            Request: request
        };

        return PostAsync<TResponse>(context);
    };

    const ExecuteRequestAsync = async <TResponse>(apiRequest: ApiRequest) => {
        const context: ApiRequestContext = {
            Method: apiRequest.Method,
            Query: false,
            Request: apiRequest.Request,
            HideError: apiRequest.HideError
        };

        let response: any = null;
        try {
            if (!apiRequest.HideLoading) {
                $CommandBus.Execute(CommandTypes.ShowBusyScreen, {});
            }
            response = await PostAsync<TResponse>(context);
        } finally {
            if (!apiRequest.HideLoading) {
                $CommandBus.Execute(CommandTypes.CloseBusyScreen, {});
            }
        }

        if (apiRequest.SuccessNotification && !Helper.IsError(response)) {
            notificationProvider.ShowInfo("İşlem başarıyla gerçekleştirildi");
        }

        return response;
    };

    const ExecuteAsync = <TResponse>(methodName: string, request: any): Promise<any> => {
        const apiRequest: ApiRequest = {
            Method: methodName,
            Request: request,
            SuccessNotification: true
        };

        return ExecuteRequestAsync<TResponse>(apiRequest);
    };

    return { ExecuteAsync, ExecuteRequestAsync, QueryAsync, GetAsync };
};
