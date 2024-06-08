import { CustomerHelper } from "../../helpers/customer-helper";
import React, { useEffect, useState } from "react";
import { CaretRightOutlined } from "@ant-design/icons";
import { CustomerInfo } from "../../data/dtos/customer/customer-info";
import { Button, Popover, Table, ColumnsType, Alert } from "interd";
import { useMultiLanguage } from "inter-ml";
import { CustomerSessionProvider } from "../../services/customer-session-provider";
import { $EventBus, EventData, EventStatuses, EventTypes } from "../../services/event-bus";
import { $CommandBus, CommandTypes } from "../../services/command-bus";
import { Helper } from "../../helpers/helper";
import { LoadingSpinner } from "../common/basic/loading-spinner";
import { RequestContext } from "../../services/request-context";
import { MaskHelper } from "../../helpers/mask-helper";

const ROOT_CLASS_NAME = "cc-app__related-customers__panel";

const renderCustomerName = (record: any) => {
    return CustomerHelper.GetCustomerFullName(record);
};
interface CustomerRelatedCustomersProps {
    search?: boolean;
}

const EventActions = {
    LoadRelatedCustomers: "CustomerRelatedCustomers.LoadRelatedCustomers"
};

export const CustomerRelatedCustomers = (props: CustomerRelatedCustomersProps) => {
    const [isVisible, setVisible] = useState(false);
    const [loading, setLoading] = useState(false);
    const [customers, setCustomers] = useState<CustomerInfo[]>([]);
    const [secondaryCustomers, setSecondaryCustomers] = useState<CustomerInfo[]>([]);
    const [anyRelatedCustomer, setAnyRelatedCustomer] = useState(false);
    const customerSessionProvider = CustomerSessionProvider();
    const requestContext = RequestContext();
    const { t } = useMultiLanguage();

    const renderDetail = (record: any) => {
        return record.Profile + "-" + CustomerHelper.GetCustomerTypeText(record.Profile, record.CustomerType);
    };

    const renderCustomerTypeText = (record: any) => {
        return CustomerHelper.GetCustomerTypeText(record.Profile, record.CustomerType);
    };

    const columns: ColumnsType<CustomerInfo> = [
        {
            title: t("CCP_CustomerNo"),
            dataIndex: "ExternalClientNo",
            key: "ExternalClientNo",
            width: 110,
            align: "center",
            render: (_, record) => {
                return requestContext.GetChannelInfo()?.MustMask
                    ? MaskHelper.MaskAll(record.ExternalClientNo?.toString())
                    : record.ExternalClientNo;
            }
        },
        {
            title: t("CCP_NameSurname"),
            dataIndex: "FullName",
            key: "FullName",
            width: 250,
            align: "center",
            render: (_, record) => {
                return renderCustomerName(record);
            }
        },
        {
            title: "MÜŞ.PRF/YAS.ST/ŞİRKET TÜRÜ",
            dataIndex: "Detail",
            key: "Detail",
            width: 200,
            align: "center",
            render: (_, record) => <span>{renderDetail(record)}</span>
        },
        {
            title: t("CCP_TypeOfCustomer"),
            dataIndex: "CustomerType",
            key: "CustomerType",
            width: 100,
            align: "center",
            render: (_, record) => <span>{renderCustomerTypeText(record)}</span>
        }
    ];

    const hasRelatedCustomer = (customerList: CustomerInfo[]): boolean => {
        const targetCustomerNumber = customerSessionProvider.GetTargetCustomerNumber()!;
        for (const customer of customerList) {
            if (customer.ExternalClientNo !== targetCustomerNumber) {
                return true;
            }
        }

        return false;
    };

    const getPrimaryRelations = (
        relations: CustomerInfo[],
        targetCustomerNumber: number,
        isIndividualCompany: boolean
    ) => {
        // Bireysel şirketlerde kendisini gösteriyoruz.
        return relations.filter(
            (relation) =>
                relation.Secondary !== true &&
                (isIndividualCompany ||
                    !props.search ||
                    (!isIndividualCompany && relation.ExternalClientNo !== targetCustomerNumber))
        );
    };

    const getSecondaryRelations = (relations: CustomerInfo[], targetCustomerNumber: number) => {
        return relations.filter(
            (relation) => relation.Secondary && (!props.search || relation.ExternalClientNo !== targetCustomerNumber)
        );
    };

    const loadRelatedCustomers = (eventData: EventData) => {
        if (eventData.Status === EventStatuses.Processing) {
            setLoading(true);
            setRelationsVisibility();
            return;
        }

        const relatedCustomers = customerSessionProvider.GetRelatedCustomers() || [];
        const targetCustomer = customerSessionProvider.GetTargetCustomer()!;
        const targetCustomerNumber = targetCustomer.ExternalClientNo;

        const isIndividualCompany = CustomerHelper.IsCustomerIndividualCompany(targetCustomer);

        const clonedRelations = [...relatedCustomers];
        const primaryRelations = getPrimaryRelations(clonedRelations, targetCustomerNumber, isIndividualCompany);
        const secondaryRelations = getSecondaryRelations(clonedRelations, targetCustomerNumber);

        setCustomers(primaryRelations);
        setSecondaryCustomers(secondaryRelations);
        setAnyRelatedCustomer(hasRelatedCustomer(primaryRelations) || hasRelatedCustomer(secondaryRelations));
        setLoading(false);
        setRelationsVisibility();
    };

    const setRelationsVisibility = () => {
        if (!customerSessionProvider.IsCallingCustomerLoaded()) {
            // Bağlı müşterilerden müşteri seçmesini sağlıyoruz.
            setVisibleInternal(true);
        }
    };

    $EventBus.SetAction(EventActions.LoadRelatedCustomers, loadRelatedCustomers);
    useEffect(() => {
        $EventBus.Subscribe(EventTypes.CustomerRelationsQueried, EventActions.LoadRelatedCustomers);
        if (!customerSessionProvider.IsTargetCustomerLoaded()) {
            return;
        }

        const targetCustomer = customerSessionProvider.GetTargetCustomer()!;
        if (CustomerHelper.IsIndividual(targetCustomer)) {
            return;
        }
    }, []);

    const toggleRelatedCustomer = () => {
        setVisibleInternal(!isVisible);
    };

    const changeRelatedCustomer = (
        customerNumber: number,
        callingCustomerNumber: number,
        hasIndividualCompanyRelation: boolean
    ) => {
        $CommandBus.Execute(CommandTypes.ChangeRelatedCustomer, {
            CustomerNumber: customerNumber,
            CallingCustomerNumber: callingCustomerNumber,
            HasIndividualCompanyRelation: hasIndividualCompanyRelation
        });
    };

    const findFirstIndividualRelation = (relatedCustomers: CustomerInfo[]): CustomerInfo | null => {
        for (const customer of relatedCustomers) {
            if (CustomerHelper.IsIndividual(customer)) {
                return customer;
            }
        }

        return null;
    };

    const processIndividualCompanyRelation = (targetCustomer: CustomerInfo, selectedRecord: CustomerInfo) => {
        if (CustomerHelper.IsIndividual(selectedRecord)) {
            // Şahıs şirketi yüklenmiş fakat bireysele geçiş yapılıyor.
            return changeRelatedCustomer(selectedRecord.ExternalClientNo, selectedRecord.ExternalClientNo, true);
        }

        // Şahıs şirketi yüklenmiş = İlişkilerden şahıs şirketi seçiyor.
        // İlk bireysel hesabını bulup onu arayan hesap olarak set etmek gerekiyor.
        let firstIndividualRelation = findFirstIndividualRelation(customers);
        if (Helper.IsNotValue(firstIndividualRelation)) {
            firstIndividualRelation = findFirstIndividualRelation(secondaryCustomers);
        }

        if (Helper.IsValue(firstIndividualRelation)) {
            // Hedef müşteri aynı sadece arayan müşteri değişimi yapılıyor.
            return changeRelatedCustomer(
                targetCustomer.ExternalClientNo,
                firstIndividualRelation!.ExternalClientNo,
                true
            );
        }

        // Bireysel müşteri bulunamazsa notifikasyon vermek gerekebilir.
    };

    const processCorporateRelation = (targetCustomer: CustomerInfo, selectedRecord: CustomerInfo) => {
        if (CustomerHelper.IsIndividual(selectedRecord)) {
            // Tüzel müşteri için arayan müşteri seçimi yapılıyor.
            return changeRelatedCustomer(targetCustomer.ExternalClientNo, selectedRecord.ExternalClientNo, false);
        }

        const callingCustomerNumber = customerSessionProvider.GetCallingCustomerNumber();
        // Tüzelden - tüzele, veya şahıs şirketine geçiş yapılıyor.
        // Arayan müşteri bir önceki müşterinin arayan müşterisi olarak set ediliyor.
        changeRelatedCustomer(selectedRecord.ExternalClientNo, callingCustomerNumber, false);
    };

    const setVisibleInternal = (visible: boolean) => {
        if (props.search) {
            return;
        }

        setVisible(visible);
    };

    const handleRowClick = (record: CustomerInfo) => {
        const targetCustomer = customerSessionProvider.GetTargetCustomer();
        if (CustomerHelper.IsIndividual(targetCustomer)) {
            // Bireysel yüklenmiş => şahıs şirketine geçiş yapılıyor.
            changeRelatedCustomer(record.ExternalClientNo, targetCustomer!.ExternalClientNo, true);
        } else if (CustomerHelper.IsCustomerIndividualCompany(targetCustomer!)) {
            processIndividualCompanyRelation(targetCustomer!, record);
        } else {
            processCorporateRelation(targetCustomer!, record);
        }

        setVisibleInternal(false);
    };

    const onRow = (record: CustomerInfo) => {
        return {
            onClick: () => handleRowClick(record)
        };
    };

    const renderRelatedCustomers = () => {
        const targetCustomer = customerSessionProvider.GetTargetCustomer()!;
        if (
            CustomerHelper.IsCorporateNotIndividualCompany(targetCustomer.Profile, targetCustomer.CustomerType) &&
            !anyRelatedCustomer
        ) {
            return <Alert description={t("CCP_Corporate_Contact")} type='error' />;
        }

        return (
            <>
                <Table
                    className='related-customers__grid'
                    columns={columns}
                    dataSource={customers}
                    pagination={false}
                    loading={loading}
                    size='small'
                    rowClassName='related-customers__grid__row'
                    rowKey={"ExternalClientNo"}
                    onRow={onRow}
                    bordered
                    title={() => t("CCP_RelatedUsers")}
                />

                {secondaryCustomers.length > 0 && (
                    <Table
                        className='related-customers__grid mt-3'
                        columns={columns}
                        dataSource={secondaryCustomers}
                        pagination={false}
                        loading={loading}
                        size='small'
                        rowClassName='related-customers__grid__row'
                        rowKey={"ExternalClientNo"}
                        onRow={onRow}
                        bordered
                        title={() => t("CCP_CorporateUsers")}
                    />
                )}
            </>
        );
    };

    const buttonCss = anyRelatedCustomer
        ? "related-customers__selection-button cc-button cc-button--success"
        : "related-customers__selection-button";

    if (loading) {
        return (
            <div className='related-customers related-customers--loading'>
                <LoadingSpinner loading={loading} size='large'></LoadingSpinner>
            </div>
        );
    }

    return (
        <div className='related-customers'>
            {props.search ? (
                <div className={ROOT_CLASS_NAME}>{renderRelatedCustomers()}</div>
            ) : (
                <Popover
                    placement='rightTop'
                    content={renderRelatedCustomers}
                    trigger='click'
                    open={isVisible}
                    onOpenChange={toggleRelatedCustomer}
                    overlayClassName={ROOT_CLASS_NAME}>
                    <Button type='primary' className={buttonCss} onClick={toggleRelatedCustomer}>
                        <span className='related-customers__LinkedAccount'>{t("CCP_LinkedAccounts")}</span>
                        <span>
                            <CaretRightOutlined className='related-customers__icon' /> 
                        </span>
                    </Button>
                </Popover>
            )}
        </div>
    );
};
