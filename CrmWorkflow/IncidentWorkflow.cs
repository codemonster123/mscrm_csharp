using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Metadata;

namespace CrmWorkflow
{
    public class CreateServiceAppointmentsFromIncidentsWorkflow : CodeActivity
    {
        [Input("Incident")]
        [ReferenceTarget("incident")]
        public InArgument<EntityReference> IncidentReference { get; set; }


        protected override void Execute(CodeActivityContext context)
        {
            ITracingService tracingSvc = context.GetExtension<ITracingService>();
            IWorkflowContext wfCtx = context.GetExtension<IWorkflowContext>();
            IOrganizationServiceFactory svcFactory = context.GetExtension<IOrganizationServiceFactory>();
            IOrganizationService service = svcFactory.CreateOrganizationService(wfCtx.UserId);

            EntityReference incidentRef = IncidentReference.Get(context);

            Entity incident = service.Retrieve("incident", incidentRef.Id, new ColumnSet());

            Guid teamid = new Guid("<some support team>");


            var leastBusyUser = ChooseBestUserOnTeam(teamid, service);


            try
            {
                var serviceappointment = new Entity();
                serviceappointment["ownerid"] = teamid;
                serviceappointment["ownershiptype"] = OwnershipTypes.TeamOwned;
                serviceappointment["resources"] = leastBusyUser.Id;
                serviceappointment["regardingobjectid"] = incident.Id;

                service.Create(serviceappointment);
                tracingSvc.Trace($"IncidentWorkflow: Created service appointment {serviceappointment.Id}");
            }
            catch(Exception ex)
            {
                tracingSvc.Trace($"IncidentWorkflow: Failed to create service appoint for incident {incident.Id}");
            }
        }
        protected Entity ChooseBestUserOnTeam(Guid teamid, IOrganizationService service)
        {
            var qryUsers = new QueryExpression("systemuser");

            var teamLink = new LinkEntity("systemuser", "teammembership", "systemuserid", "systemuserid", JoinOperator.Inner);
            var teamCondition = new ConditionExpression("teamid", ConditionOperator.Equal, teamid);
            teamLink.LinkCriteria.AddCondition(teamCondition);

            qryUsers.LinkEntities.Add(teamLink);

            var usersInTeam = service.RetrieveMultiple(qryUsers);
            var leastBusyUser = usersInTeam.Entities.FirstOrDefault();

            return leastBusyUser;
        }
    }
}
