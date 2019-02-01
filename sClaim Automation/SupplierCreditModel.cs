using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static sClaim_Automation.SupplierCreditReqParam;
using System.Windows.Forms;

namespace sClaim_Automation
{
    class SupplierCreditModel
    {
        string _curlURL;
        string _xmlnsURL;
        string _headerURL;
        string _responsibility;
        string _respApplication;
        string _securityGroup;
        string _orgID;
        string _nlsLanguage;

        RightNowConnectService _rnConnectService;
        WorkspaceAddIn _wsAddinObject = null;
        List<int> _supplierCreditIDs = new List<int>();

        public SupplierCreditModel()
        {
            _rnConnectService = RightNowConnectService.GetService();
            string partsOrderConfigValue = _rnConnectService.GetConfigValue("CUSTOM_CFG_SUPPLIER_CREDIT");
            if (partsOrderConfigValue != null)
            {
                var s = new JavaScriptSerializer();

                var configVerb = s.Deserialize<WebServiceConfigVerbs.RootObject>(partsOrderConfigValue);
                _curlURL = configVerb.URL;
                _headerURL = configVerb.xmlns;
                _xmlnsURL = configVerb.RESTHeader.xmlns;
                _respApplication = configVerb.RESTHeader.RespApplication;
                _responsibility = configVerb.RESTHeader.Responsibility;
                _securityGroup = configVerb.RESTHeader.SecurityGroup;
                _nlsLanguage = configVerb.RESTHeader.NLSLanguage;
                _orgID = configVerb.RESTHeader.Org_Id;
            }
        }

        /// <summary>
        /// Method which is called to get casual supplier credit memo info
        /// </summary>
        /// <param name="wsAddin">WorkspaceAddin instance</param>
        public void GetSupplierCreditInfo(WorkspaceAddIn wsAddin)
        {
            try
            {
                _wsAddinObject = wsAddin;
                string operatingUnit = "";
                string busInfo = "";
                //Get Incident Type
                string incType = _wsAddinObject.GetIncidentField("c", "Incident Type");
                if (incType == "30")//case of claim type, bus is stored at incident parent level
                {
                    string busID = _wsAddinObject.GetIncidentField("CO", "Bus");
                    if(busID != "")
                        //Get Customer ID and VIN
                        busInfo = _rnConnectService.GetBusInfo(Convert.ToInt32(busID), _wsAddinObject._sClaimRecord.Id);
                }
                else// Case of reported Inc, then BUS is stored at incident_vin level
                {
                    //Get Customer ID and VIN
                    busInfo = _rnConnectService.GetBusInfo(0, _wsAddinObject._sClaimRecord.Id);//Customer ID and VIN
                }

                string sClaimOrgID = _wsAddinObject.GetsClaimField("Organization");//Supplier ID
                
                string sClaimRefNum = _wsAddinObject.GetsClaimField("sclaim_ref_num");

                string[] supplierCredits = RightNowConnectService.GetService().GetSupplierCreditInfo(_wsAddinObject._sClaimRecord.Id);

                if(supplierCredits == null)
                {
                    _wsAddinObject.InfoLog("No new Credit Memo found");
                    return;
                }
                List<PCMHEADERREC> cmRecordList = new List<PCMHEADERREC>();//credit memo record list
                foreach (string supplierCredit in supplierCredits)//loop over all credit memo mapped with sClaim
                {
                    string[] supplierCreditInfo = supplierCredit.Split('~');
                    if (supplierCreditInfo[6] == "NFA")
                    {
                        operatingUnit = "SCL";
                    }
                    else if (supplierCreditInfo[6] == "NFI")
                    {
                        operatingUnit = "WPR";
                    }
                    else
                    {
                        wsAddin.InfoLog("'Credit To' is not set in supplier credit record");
                        return;
                    }
                    string supplierInfo = _rnConnectService.GetSupplierInfo(Convert.ToInt32(sClaimOrgID), operatingUnit);
                    if (supplierInfo == null)
                    {
                        wsAddin.InfoLog("No Info found for pay site");
                        return;
                    }

                    _supplierCreditIDs.Add(Convert.ToInt32(supplierCreditInfo[5]));// store the supplier credit record ids

                    PCMHEADERREC cmRecord = new PCMHEADERREC();//credit memo record
                    if (supplierCreditInfo[1] != "")
                    {
                        cmRecord.CLAIM_AMOUNT = Convert.ToDouble(supplierCreditInfo[1]);
                    }
                    cmRecord.VIN_NUMBER = busInfo.Split('~')[1];
                    cmRecord.CREDIT_MEMO_NO = supplierCreditInfo[0];
                    cmRecord.CURRENCY = supplierCreditInfo[2];
                    cmRecord.CUSTOMER_ID = Convert.ToInt32(busInfo.Split('~')[0]);
                    cmRecord.SCLAIM_NO = sClaimRefNum;
                    if(supplierCreditInfo[3] != "")
                    {
                        cmRecord.TAX_AMOUNT = Convert.ToDouble(supplierCreditInfo[3]);
                    }
                    cmRecord.TAX_CODE = supplierCreditInfo[4];
                    cmRecord.SUPPLIER_ID = Convert.ToInt32(supplierInfo.Split('~')[0]);
                    cmRecord.SUPPLIER_SITE_ID = Convert.ToInt32(supplierInfo.Split('~')[1]);
                    
                    cmRecordList.Add(cmRecord);
                }
                var content = GetRequestParam(cmRecordList);

                //Call supplier Credit web-service
                var jsonContent = WebServiceRequest.JsonSerialize(content);
                jsonContent = jsonContent.Replace("xmlns", "@xmlns");
                string jsonResponse = WebServiceRequest.Get(_curlURL, jsonContent, "POST");

                if (jsonResponse == "")
                {
                    wsAddin.InfoLog("Server didn't returned any info");
                    return;
                }
                else
                {
                    ExtractResponse(jsonResponse, wsAddin);
                }

            }
            catch (Exception ex)
            {
                wsAddin.InfoLog(ex.Message);
            }
        }

        /// <summary>
        /// Funtion to frame request parameter 
        /// </summary>
        /// <param name="cmRecordList">Credit Memo records</param>
        /// <returns>RootObject instance</returns>
        public RootObject GetRequestParam(List<PCMHEADERREC> cmRecordList)
        {
            //forming request paramter for supplier credit web-service
            var content = new RootObject
            {
                CREATE_CREDIT_MEMOS_Input = new CREATECREDITMEMOSInput
                {
                    xmlns = _xmlnsURL,
                    RESTHeader = new RESTHeader
                    {
                        @xmlns = _headerURL,
                        Responsibility = _responsibility,
                        RespApplication = _respApplication,
                        SecurityGroup = _securityGroup,
                        Org_Id = _orgID,
                        NLSLanguage = _nlsLanguage

                    },
                    InputParameters = new InputParameters
                    {
                        P_CM_HEADER_TBL = new PCMHEADERTBL
                        {
                            P_CM_HEADER_REC = cmRecordList
                        }
                    }
                }
            };

            return content;
        }
        /// <summary>
        /// Funtion which gets call to handle ebs webservice response
        /// </summary>
        /// <param name="respJson">response in jSON string</param>
        /// <param name="woAddin">WorkspaceAddin instance</param>
        public void ExtractResponse(string respJson, WorkspaceAddIn woAddin)
        {
            // decode the Json response
            respJson = respJson.Replace("@xmlns:xsi", "@xmlns_xsi");
            respJson = respJson.Replace("@xsi:nil", "@xsi_nil");
            Dictionary<string, object> data = (Dictionary<string, object>)WebServiceRequest.JsonDeSerialize(respJson);
            Dictionary<string, object> output = (Dictionary<string, object>)data["OutputParameters"];
            Dictionary<string, object> cmReturnTbl = (Dictionary<string, object>)output["P_CM_RETURN_TBL"];

            //update supplier credit record info
            RightNowConnectService.GetService().upadteSupplierCredit((object[])cmReturnTbl["P_CM_RETURN_TBL_ITEM"], _supplierCreditIDs);
        }
    }
}
