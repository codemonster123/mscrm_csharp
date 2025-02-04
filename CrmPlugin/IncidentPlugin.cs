using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace CrmPlugin
{
    public class IncidentPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingSvc = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

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
                var entity = (Entity)ctx.InputParameters["Target"];
                if (entity.LogicalName.ToLower() != "incident")
                {
                    // We're not dealing with an Incident Entity, so should ignore
                    return;
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
