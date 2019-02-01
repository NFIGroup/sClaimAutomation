using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Windows.Forms;
using sClaim_Automation.RightNowService;
using RightNow.AddIns.AddInViews;

namespace sClaim_Automation
{
    class RightNowConnectService
    {
        private static RightNowConnectService _rightnowConnectService;
        private static object _sync = new object();
        private static RightNowSyncPortClient _rightNowClient;
        private RightNowConnectService()
        {

        }
        public static RightNowConnectService GetService()
        {
            if (_rightnowConnectService != null)
            {
                return _rightnowConnectService;
            }

            try
            {
                lock (_sync)
                {
                    if (_rightnowConnectService == null)
                    {
                        // Initialize client with current interface soap url 
                        string url = WorkspaceAddIn._globalContext.GetInterfaceServiceUrl(ConnectServiceType.Soap);
                        EndpointAddress endpoint = new EndpointAddress(url);

                        BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential);
                        binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;

                        // Optional depending upon use cases
                        binding.MaxReceivedMessageSize = 1024 * 1024;
                        binding.MaxBufferSize = 1024 * 1024;
                        binding.MessageEncoding = WSMessageEncoding.Mtom;

                        _rightNowClient = new RightNowSyncPortClient(binding, endpoint);

                        BindingElementCollection elements = _rightNowClient.Endpoint.Binding.CreateBindingElements();
                        elements.Find<SecurityBindingElement>().IncludeTimestamp = false;
                        _rightNowClient.Endpoint.Binding = new CustomBinding(elements);
                        WorkspaceAddIn._globalContext.PrepareConnectSession(_rightNowClient.ChannelFactory);

                        _rightnowConnectService = new RightNowConnectService();
                    }

                }
            }
            catch (Exception e)
            {
                _rightnowConnectService = null;
                MessageBox.Show(e.Message);
            }

            return _rightnowConnectService;
        }
        /// <summary>
        /// Return individual fields as per query
        /// </summary>
        /// <param name="ApplicationID"></param>
        /// <param name="Query"></param>
        /// <returns> array of string delimited by '~'</returns>
        private string[] GetRNData(string ApplicationID, string Query)
        {
            string[] rnData = null;
            ClientInfoHeader hdr = new ClientInfoHeader() { AppID = ApplicationID };

            byte[] output = null;
            CSVTableSet data = null;

            try
            {
                data = _rightNowClient.QueryCSV(hdr, Query, 1000, "~", false, false, out output);
                string dataRow = String.Empty;
                if (data != null && data.CSVTables.Length > 0 && data.CSVTables[0].Rows.Length > 0)
                {
                    return data.CSVTables[0].Rows;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
            return rnData;
        }
        /// <summary>
        /// Get Config Value based on lookupName
        /// </summary>
        /// <param name="configLookupName"></param>
        /// <returns>config value</returns>
        public string GetConfigValue(string configLookupName)
        {
            string query = "select Configuration.Value from Configuration where lookupname = '" + configLookupName + "'";
            string[] resultSet = GetRNData("Configuartion Value", query);
            if (resultSet != null && resultSet.Length > 0)
            {
                var jsonTrim = resultSet[0].Replace("\"\"", "\"");

                // jsonString has extra " at start, end and each " 
                int i = jsonTrim.IndexOf("\"");
                int j = jsonTrim.LastIndexOf("\"");
                var finalJson = jsonTrim.Substring(i + 1, j - 1);
                finalJson = finalJson.Replace("@xmlns", "xmlns");

                return finalJson;
            }
            return null;
        }
  
        /// <summary>
        /// Return supplier info 
        /// </summary>
        /// <param name="orgID">sClaim Org ID</param>
        /// <returns>supplier/org Ebs ID and Supplier Site(pay one) ID</returns>
        public string GetSupplierInfo(int orgID, string operatingUnit)
        {
            // Commneting this old query based on #1982 share point item
           // string queryString = "SELECT Organization.CustomFields.CO.ebs_id_org as orgEbsID, s.ebs_id_site as siteEBSID"+
            //                     " FROM CO.Site s WHERE s.pay_site = 1 AND s.Operating_Unit=" + "'" + operatingUnit + "'" + " AND s.Organization = " + orgID + " LIMIT 1";
            string queryString = "SELECT Organization.CustomFields.CO.ebs_id_org as orgEbsID, s.ebs_id_site as siteEBSID" +
                                  " FROM CO.Site s WHERE s.site_name = 'OSVC-WARRANTY' AND s.Operating_Unit=" + "'" + operatingUnit + "'" + " AND  s.Organization = " + orgID + " LIMIT 1";

            
            string[] rowData = GetRNData("Get Supplier Info", queryString);
            if (rowData != null && rowData.Length > 0)
            {
                foreach (string data in rowData)
                {
                    return data;
                }
            }
            return null;
        }
        /// <summary>
        /// Return Bus VIN and Buw Owner EBS ID
        /// </summary>
        /// <param name="busID">Bus ID</param>
        /// <param name="sclaimID">sClaim ID</param>
        /// <returns>Vin and ebs_org_id</returns>
        public string GetBusInfo(int busID, int sclaimID)
        {
            string queryString = "";
            if (busID != 0)
            {
                queryString = "SELECT b.sales_release.organization.CustomFields.CO.ebs_id_org as customerEbsID," +
                                 " b.vin " + "FROM CO.Bus b WHERE b.ID = " + busID;
            }
            else
            {
                queryString = "SELECT s.Work_Order.Incident_VIN_ID.Bus.sales_release.organization.CustomFields.CO.ebs_id_org, "
                             +"s.Work_Order.Incident_VIN_ID.Bus.vin "
                             +"FROM CO.sClaim s WHERE ID ="+ sclaimID;
            }             

            string[] rowData = GetRNData("Get bus owner info", queryString);
            if (rowData != null && rowData.Length > 0)
            {
                foreach (string data in rowData)
                {
                    return data;
                }
            }
            return null;
        }
        /// <summary>
        /// Get Supplier Credit Memo detail for record that not been sent to EBS or send with an error 
        /// </summary>
        /// <param name="ebsOrgID">Custom field value</param>
        /// <returns>Orgs ID</returns>
        public string[] GetSupplierCreditInfo(int sClaimID)
        {
            
            string queryString = "SELECT credit_memo_nmbr, supplier_credit_amt, currency.LookupName, tax_amount, tax_code, ID, Credit_To.LookupName " +
                                 "FROM CO.SupplierCredits WHERE sClaim ="+sClaimID+" AND (ebs_status IS NULL OR ebs_status = 'E')";
            string[] rowData = GetRNData("Get Supplier Credit Info", queryString);
            if (rowData != null && rowData.Length > 0)
            {
                return rowData;
            }
            return null;
        }
        /// <summary>
        /// Update "Supplier Credit" child record of sClaim 
        /// </summary>
        /// <param name="cmReturnItems">response of BOM query web-service</param>
        /// <param name="scID">supplier Credit record ID</param>
        public void upadteSupplierCredit(object[] cmReturnItems, List<int> scIDs)
        {
            try
            {
                //cmReturnItems.
                List<RNObject> rnObjs = new List<RNObject>();
                int ii = 0;
                foreach (int scID in scIDs)//loop over each supplier credit IDs
                {
                    Dictionary<string, object> cmReturnItem = (Dictionary<string, object>)cmReturnItems[ii++];
                    List<GenericField> gfs = new List<GenericField>();

                    GenericObject gnObject = new GenericObject();
                    RNObjectType objType = new RNObjectType();
                    objType.Namespace = "CO";
                    objType.TypeName = "SupplierCredits";
                    gnObject.ObjectType = objType;

                    ID goID = new ID();
                    goID.id = scID;
                    goID.idSpecified = true;
                    gnObject.ID = goID;

                    if (cmReturnItem["RETURN_STATUS"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("ebs_status", createStringDataValue(cmReturnItem["RETURN_STATUS"].ToString()), DataTypeEnum.STRING));
                        if (cmReturnItem["RETURN_STATUS"].ToString() == "S")
                        {
                            gfs.Add(createGenericField("date_processed", createDateDataValue(DateTime.Now.ToShortDateString()), DataTypeEnum.DATE));
                        }
                    }
                    if (cmReturnItem["CONVERTED_CURRENCY"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("converted_currency", createNamedIDDataValueForName(cmReturnItem["CONVERTED_CURRENCY"].ToString()), DataTypeEnum.NAMED_ID));
                    }
                    if (cmReturnItem["CONVERTED_AMOUNT"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("converted_amount", createStringDataValue(cmReturnItem["CONVERTED_AMOUNT"].ToString()), DataTypeEnum.STRING));
                    }
                    if (cmReturnItem["CONVERTED_TAX_CURRENCY"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("converted_tax_currency", createNamedIDDataValueForName(cmReturnItem["CONVERTED_TAX_CURRENCY"].ToString()), DataTypeEnum.NAMED_ID));
                    }
                    if (cmReturnItem["CONVERTED_TAX_AMOUNT"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("converted_tax_amount", createStringDataValue(cmReturnItem["CONVERTED_TAX_AMOUNT"].ToString()), DataTypeEnum.STRING));
                    }
                    if (cmReturnItem["RETURN_MESSAGE"].ToString().Trim().Length > 0)
                    {
                        gfs.Add(createGenericField("ebs_message", createStringDataValue(cmReturnItem["RETURN_MESSAGE"].ToString()), DataTypeEnum.STRING));
                    }

                    gnObject.GenericFields = gfs.ToArray();

                    rnObjs.Add(gnObject);
                }
                callBatchJob(getUpdateMsg(rnObjs));//update supplier credit
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return;
        }

       #region Miscellaneous

            /// <summary>
            /// Perform Batch operation
            /// </summary>
            /// <param name="partMsg"></param>
            /// <param name="laborMsg"></param>
        public void callBatchJob(Object msg)
        {
            try
            {
                /*** Form BatchRequestItem structure ********************/

                BatchRequestItem[] requestItems = new BatchRequestItem[1];

                BatchRequestItem requestItem = new BatchRequestItem();

                requestItem.Item = msg;

                requestItems[0] = requestItem;
                requestItems[0].CommitAfter = true;
                requestItems[0].CommitAfterSpecified = true;
                /*********************************************************/


                ClientInfoHeader clientInfoHeader = new ClientInfoHeader();
                clientInfoHeader.AppID = "Batcher";

                BatchResponseItem[] batchRes = _rightNowClient.Batch(clientInfoHeader, requestItems);
                //If response type is RequestErrorFaultType then show the error msg 
                if (batchRes[0].Item.GetType().Name == "RequestErrorFaultType")
                {
                    RequestErrorFaultType requestErrorFault = (RequestErrorFaultType)batchRes[0].Item;
                    MessageBox.Show("There is an error with batch job :: " + requestErrorFault.exceptionMessage);
                }
            }
            catch (FaultException ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        /// <summary>
        /// To create Update Message for Batch
        /// </summary>
        /// <param name="coList"></param>
        /// <returns></returns>
        private UpdateMsg getUpdateMsg(List<RNObject> coList)
        {
            UpdateMsg updateMsg = new UpdateMsg();
            UpdateProcessingOptions updateProcessingOptions = new UpdateProcessingOptions();
            updateProcessingOptions.SuppressExternalEvents = true;
            updateProcessingOptions.SuppressRules = true;
            updateMsg.ProcessingOptions = updateProcessingOptions;

            updateMsg.RNObjects = coList.ToArray();

            return updateMsg;
        }

        /// <summary>
        /// To create Create Message for Batch
        /// </summary>
        /// <param name="coList"></param>
        /// <returns></returns>
        private CreateMsg getCreateMsg(List<RNObject> coList)
        {
            CreateMsg createMsg = new CreateMsg();
            CreateProcessingOptions createProcessingOptions = new CreateProcessingOptions();
            createProcessingOptions.SuppressExternalEvents = true;
            createProcessingOptions.SuppressRules = true;
            createMsg.ProcessingOptions = createProcessingOptions;

            createMsg.RNObjects = coList.ToArray();

            return createMsg;
        }

        /// <summary>
        /// Create string type data value
        /// </summary>
        /// <param name="val"></param>
        /// <returns> DataValue</returns>
        private DataValue createStringDataValue(string val)
        {
            DataValue dv = new DataValue();
            dv.Items = new Object[] { val };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.StringValue };  //Change this to the type of field
            return dv;
        }
        /// <summary>
        /// Create string type data value
        /// </summary>
        /// <param name="val"></param>
        /// <returns> DataValue</returns>
        private DataValue createDateDataValue(string val)
        {
            DateTime date = (Convert.ToDateTime(val));
            DataValue dv = new DataValue();
            dv.Items = new Object[] { date };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.DateValue };  //Change this to the type of field
            return dv;
        }
        /// <summary>
        /// Create Boolean type data value
        /// </summary>
        /// <param name="val"></param>
        /// <returns> DataValue</returns>
        private DataValue createBooleanDataValue(Boolean val)
        {
            DataValue dv = new DataValue();
            dv.Items = new Object[] { val };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.BooleanValue };

            return dv;
        }

        /// <summary>
        /// Create integer type data value
        /// </summary>
        /// <param name="val"></param>
        /// <returns> DataValue</returns>
        private DataValue createIntegerDataValue(int val)
        {
            DataValue dv = new DataValue();
            dv.Items = new Object[] { val };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.IntegerValue };  //Change this to the type of field
            return dv;
        }

        /// <summary>
        /// Create GenericField object
        /// </summary>
        /// <param name="name">Name Of Generic Field</param>
        /// <param name="dataValue">Vlaue of generic field</param>
        /// <param name="type">Type of generic field</param>
        /// <returns> GenericField</returns>
        private GenericField createGenericField(string name, DataValue dataValue, DataTypeEnum type)
        {
            GenericField genericField = new GenericField();

            genericField.dataType = type;
            genericField.dataTypeSpecified = true;
            genericField.name = name;
            genericField.DataValue = dataValue;
            return genericField;
        }

        /// <summary>
        /// Create Named ID type Data Value for NamedID as input
        /// </summary>
        /// <param name="namedvalue"></param>
        /// <returns></returns>
        private DataValue createNamedID(NamedID namedvalue)
        {
            DataValue dv = new DataValue();
            dv.Items = new Object[] { namedvalue };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.NamedIDValue };
            return dv;
        }

        /// <summary>
        /// Create Named ID type data value for integer value as input
        /// </summary>
        /// <param name="idVal"></param>
        /// <returns> DataValue</returns>
        private DataValue createNamedIDDataValue(long idVal)
        {
            ID id = new ID();
            id.id = idVal;
            id.idSpecified = true;

            NamedID namedID = new NamedID();
            namedID.ID = id;

            DataValue dv = new DataValue();
            dv.Items = new Object[] { namedID };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.NamedIDValue };

            return dv;
        }
        /// <summary>
        /// Create Named ID type data value for Name
        /// </summary>
        /// <param name="name"></param>
        /// <returns> DataValue</returns>
        private DataValue createNamedIDDataValueForName(string name)
        {
            NamedID namedID = new NamedID();
            namedID.Name = name;

            DataValue dv = new DataValue();
            dv.Items = new Object[] { namedID };
            dv.ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.NamedIDValue };

            return dv;
        }
        #endregion
    }
}
