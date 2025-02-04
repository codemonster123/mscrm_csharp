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

            Guid lowPriorityQuestionTeamId = new Guid("<some support team for low (3) priority question>");
            Guid mediumPriorityQuestionTeamId = new Guid("<some support team for normal (2) priority question>");
            Guid highPriorityQuestionTeamId = new Guid("<some support team for high (1) priority question");
            Guid lowPriorityProblemTeamId = new Guid("<some support team for low (3) priority problem>");
            Guid mediumPriorityProblemTeamId = new Guid("<some support team for normal (2) priority problem>");
            Guid highPriorityProblemTeamId = new Guid("<some support team for high (1) priority problem");
            Guid lowPriorityRequestTeamId = new Guid("<some support team for low (3) priority request>");
            Guid mediumPriorityRequestTeamId = new Guid("<some support team for normal (2) priority request>");
            Guid highPriorityRequestTeamId = new Guid("<some support team for high (1) priority request");

            Guid teamIdFromPriorityAndCaseType(object priority, object caseType)
            {
                int intPriority = 0; 
                int intCaseType = 0;

                if (!int.TryParse(caseType?.ToString(), out intCaseType))
                {
                    tracingSvc.Trace($"CreateServiceAppointmentsFromIncidentsWorkflow: Incident {incident.Id} has bad casetype of '{caseType}'");
                    throw new WorkflowApplicationException($"CreateServiceAppointmentsFromIncidentsWorkflow: Incident {incident.Id} has bad casetype of '{caseType}'");
                }
                if (!int.TryParse(priority?.ToString(), out intPriority)) {

                    tracingSvc.Trace($"CreateServiceAppointmentsFromIncidentsWorkflow: Non-numeric priority value '{priority}' for incident {incident.Id}");
                    intPriority = 2;
                } else if (intPriority < 1 || intPriority > 3)
                {
                    tracingSvc.Trace($"CreateServiceAppointmentsFromIncidentsWorkflow: Bad priority value '{priority}' for incident {incident.Id}");
                    intPriority = 2;

                }

                switch (intPriority) {
                    case 1:
                        switch (intCaseType)
                        {
                            case 1: //Question
                                return highPriorityQuestionTeamId;
                            case 2: //Problem
                                return highPriorityProblemTeamId;
                            case 3: //Request
                                return highPriorityRequestTeamId;
                        }
                        break;
                    case 2:
                        switch (intCaseType)
                        {
                            case 1: //Question
                                return mediumPriorityQuestionTeamId;
                            case 2: //Problem
                                return mediumPriorityProblemTeamId;
                            case 3: //Request
                                return mediumPriorityRequestTeamId;

                        }
                        break;
                    case 3:
                        switch (intCaseType)
                        {
                            case 1: //Question
                                return lowPriorityQuestionTeamId;
                            case 2: //Problem
                                return lowPriorityProblemTeamId;
                            case 3: //Request
                                return lowPriorityRequestTeamId;
                        }
                        break;
                }
                throw new WorkflowApplicationException($"CreateServiceAppointmentsFromIncidentsWorkflow: Incident {incident.Id} " +
                    $"has unaccounted for casetype '{caseType}' or priority '{priority}'");
            }

            try
            {
                var serviceappointment = new Entity();
                var chosenTeamId = teamIdFromPriorityAndCaseType(incident["prioritycode"], incident["casetype"]);
                serviceappointment["ownerid"] = chosenTeamId;
                serviceappointment["ownershiptype"] = OwnershipTypes.TeamOwned;
                serviceappointment["resources"] = ChooseBestUserOnTeam(chosenTeamId, service);
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
