﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using System.Linq.Expressions;

namespace CrmPlugin
{
    public class IncidentPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingSvc = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            void LogValidationError(Guid incidentId, string validationError)
            {
                tracingSvc.Trace($"IncidentPlugin: {validationError} for incident id {incidentId}");
            }
            bool ValidateIncident(Entity incident)
            {
                var failedValidation = false;
                if (string.IsNullOrEmpty(incident["emailaddress"] as string))
                {
                    LogValidationError(incident.Id, "Missing email address");
                    failedValidation = true;
                }
                if (string.IsNullOrEmpty(incident["title"] as string))
                {
                    LogValidationError(incident.Id, "Missing title");
                    failedValidation = true;
                }
                if (incident["customerid"] is null)
                {
                    LogValidationError(incident.Id, "Missing customer id");
                    failedValidation = true;
                }

                return !failedValidation;
            }

            IPluginExecutionContext ctx = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            if (ctx == null)
            {
                tracingSvc.Trace($"IncidentPlugin: IPluginExecutionContext is null");
            }

            if (ctx.InputParameters.Contains("Target") &&
                ctx.InputParameters["Target"] is Entity &&
                new string[] { "create", "update" }.Contains(ctx.MessageName.ToLower().Trim()) &&
                (ctx.InputParameters["Target"] as Entity)?.LogicalName == "incident"
                )
            {
                var incident = (Entity)ctx.InputParameters["Target"];

                if (!ValidateIncident(incident))
                {
                    throw new InvalidPluginExecutionException($"Incident with id {incident.Id} failed validation.");
                }

                var statecode = incident["statecode"] as OptionSetValue;
                if (statecode == null 
                    || (statecode.Value != 0 /*Active*/ 
                        && statecode.Value != 1 /*Resolved*/))
                {
                    // Ignore cancelled incidents, that is, statecode == 2
                    return;
                }
                Entity preTarget = default;
                if (ctx.PreEntityImages.Contains("PreEntityImage"))
                {
                    preTarget = ctx.PreEntityImages["PreEntityImage"];
                }
                Entity postTarget = default;
                if (ctx.PostEntityImages.Contains("PostEntityImage"))
                {
                    postTarget = ctx.PostEntityImages["PostEntityImage"];
                }
                if (preTarget != default && postTarget != default)
                {
                    if (preTarget["statuscode"] != postTarget["statuscode"])
                    {
                        var now = DateTime.UtcNow;
                        incident["new_prior_statuscode"] = preTarget["statuscode"];
                        incident["new_statuscode_lastupdated"] = now;
                        if (incident["new_statuscode_change_notified_cust_on"] != null 
                            && now.CompareTo(incident["new_statuscode_change_notified_cust_on"]) < 0)
                        {
                            // Should not find future timestamps
                            tracingSvc.Trace($"IncidentPlugin: new_statuscode_change_notified_cust_on found to be in the future!");
                        }
                        
                    }
                }

                IOrganizationServiceFactory svcFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                if (svcFactory == null)
                {
                    tracingSvc.Trace($"IncidentPlugin: IOrganizationServiceFactory is null");
                }
                IOrganizationService service = svcFactory.CreateOrganizationService(ctx.UserId);
                if (service == null)
                {
                    tracingSvc.Trace($"IncidentPlugin: IOrganizationService is null");
                }

                try
                {
                    //If we need complex logic or more expensive types of validation, put it here.
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("Error occurred in IncidentPlugin");
                }
                catch(Exception ex)
                {
                    tracingSvc.Trace($"Error with IncidentPlugin: {ex.Message.ToString()}");
                }

            }
        }
    }
}
