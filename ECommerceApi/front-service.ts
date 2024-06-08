import { ApiCaller } from "./api-caller";
import { ControllerTypes } from "../data/enums/controller-types";
import { CurrencyData } from "../data/dtos/currency/currency-data";

export const CurrencyService = () => {
    const currencyApiCaller = ApiCaller(ControllerTypes.Currency);

    const getCurrency = (currencyCode: number): Promise<CurrencyData> => {
        return currencyApiCaller.QueryAsync("GetCurrencyInLocalCurrency", { CurrencyCode: currencyCode });
    };

    return {
        GetCurrency: getCurrency
    };
};
