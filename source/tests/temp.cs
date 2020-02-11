namespace Buhler.IoT.Backend.CloudGateway.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Buhler.IoT.ApplicationLogger.Interfaces;
    using Buhler.IoT.Backend.CloudGateway.Api.Config;
    using Buhler.IoT.Backend.CloudGateway.Api.Enums;
    using Buhler.IoT.Backend.CloudGateway.Api.Models;
    using Buhler.IoT.Backend.CloudGateway.Api.Repositories;
    using Newtonsoft.Json.Linq;
    using NJsonSchema.Validation;

    public class OrchestrateService : IOrchestrateService
    {
        private const string AgentonfigurationNameTemplate = "{0}_{1}";
        private const string DefaultUpstreamModule = "iot-edge-module-upstream";
        private readonly ILoggerTransaction _loggerTransaction;
        private readonly IEdgeAgentRepository _edgeAgentRepository;
        private readonly IEdgeMessagePipelineRepository _edgeMessagePipelineRepository;
        private readonly IEdgeConfigurationRepository _edgeConfigurationRepository;
        private readonly IEdgeGatewayConfigurationRepository _edgeGatewayConfigurationRepository;
        private readonly IEdgeAgentDeploymentRepository _edgeAgentDeploymentRepository;
        private readonly IConfigurationService _configuration;
        private readonly IEdgeIoTHubRepository _ioTHubRepository;
        private readonly IOrchestrateRepository _orchestrateRepository;
        private readonly IEdgeMessagePipelineService _edgeMessagePipelineService;
        private readonly IModuleService _moduleService;

        public OrchestrateService(
           ILoggerTransaction loggerTransaction,
           IConfigurationService configuration,
           IEdgeAgentDeploymentService edgeAgentDeploymentService,
           IEdgeMessagePipelineService edgeMessagePipelineService,
           IModuleService moduleService)
        {
            _loggerTransaction = loggerTransaction;
            _configuration = configuration;
            _edgeAgentRepository = edgeAgentDeploymentService.EdgeAgentRepository;
            _edgeMessagePipelineRepository = edgeAgentDeploymentService.EdgeMessagePipelineRepository;
            _edgeConfigurationRepository = edgeAgentDeploymentService.EdgeConfigurationRepository;
            _edgeGatewayConfigurationRepository = edgeAgentDeploymentService.EdgeGatewayConfigurationRepository;
            _edgeAgentDeploymentRepository = edgeAgentDeploymentService.EdgeAgentDeploymentRepository;
            _ioTHubRepository = edgeAgentDeploymentService.IoTHubRepository;
            _orchestrateRepository = edgeAgentDeploymentService.OrchestrateRepository;
            _edgeMessagePipelineService = edgeMessagePipelineService;
            _moduleService = moduleService;
        }

        public async Task<EdgeAgentDeploymentPayload> PrepareDeploymentTemplateForInternalAgentIdAsync(string plantId, string internalAgentId)
        {
            using (ILoggerTransaction logger = _loggerTransaction.LogTransaction(GetType().Name))
            {
                try
                {
                    EdgeAgentDeploymentPayload agentPayload = new EdgeAgentDeploymentPayload();
                    JTokenWrapper deploymentJsonObject = new JTokenWrapper();
                    var deploymentTemplate = await _edgeAgentDeploymentRepository.GetDeploymentTemplateAsync();
                    deploymentJsonObject.Value = JObject.Parse(deploymentTemplate.DeploymentTemplate);

                    var edgeAgent = await _edgeAgentRepository.GetEdgeAgentByIdAsync(plantId, internalAgentId);
                    var edgeConfiguration = await _edgeConfigurationRepository.GetEdgeConfigurationByInternalAgentIdAsync(plantId, internalAgentId);
                    var edgeGatewayConfiguration = await _edgeGatewayConfigurationRepository.GetEdgeGatewayConfigurationByInternalAgentIdAsync(plantId, internalAgentId);

                    var modulesOfAgent = await _edgeMessagePipelineRepository.GetEdgeModuleConfigurationsByAgentIdAsync(plantId, internalAgentId);
                    var defaultModulesOfAgent = await GetDefaultModulesOfAgentAsync(plantId, internalAgentId);
                    var finalModulesOfAgent = GetFinalModulesOfAgent(defaultModulesOfAgent, modulesOfAgent);

                    var routesForTemplate = await GetRoutesForTemplateAsync(plantId, internalAgentId, defaultModulesOfAgent, finalModulesOfAgent);
                    var modulesForTemplate = GetModulesForTemplate(defaultModulesOfAgent, modulesOfAgent, edgeConfiguration);

                    JObject modules = JObject.FromObject(modulesForTemplate);
                    var edgeAgentDesiredPropertiesModules = GetEdgeAgentDesiredPropertiesModules(deploymentJsonObject).Value;
                    edgeAgentDesiredPropertiesModules.Replace(modules);

                    JObject routes = JObject.FromObject(routesForTemplate);
                    var edgeHubDesiresPropertiesRoutes = GetEdgeHubDesiredPropertiesRoutes(deploymentJsonObject).Value;
                    edgeHubDesiresPropertiesRoutes.Replace(routes);

                    SetModuleDesiredProperties(finalModulesOfAgent, deploymentJsonObject, edgeAgent, edgeConfiguration);
                    UpdateEdgeHub(deploymentJsonObject, edgeConfiguration, edgeGatewayConfiguration);
                    UpdateEdgeAgent(deploymentJsonObject, edgeConfiguration, edgeGatewayConfiguration);
                    SetRegistryCredentials(deploymentJsonObject);
                    agentPayload.DeploymentTemplate = deploymentJsonObject.Value;
                    agentPayload.AgentConfigurationName = string.Format(AgentonfigurationNameTemplate, edgeAgent.AgentName, DateTime.UtcNow.ToString());
                    agentPayload.DeploymentConfiguration = await PrepareDeploymentConfigurationForInternalAgentIdAsync(plantId, internalAgentId);

                    return agentPayload;
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    throw;
                }
            }
        }

        public async Task<EdgeAgentDeploymentConfiguration> PrepareDeploymentConfigurationForInternalAgentIdAsync(string plantId, string internalAgentId)
        {
            using (ILoggerTransaction logger = _loggerTransaction.LogTransaction(GetType().Name))
            {
                try
                {
                    var edgeAgent = await _edgeAgentRepository.GetEdgeAgentByIdAsync(plantId, internalAgentId);
                    EdgeAgentDeploymentConfiguration edgeAgentConfigurationTemplate = new EdgeAgentDeploymentConfiguration();
                    edgeAgentConfigurationTemplate.Name = edgeAgent.AgentName;
                    edgeAgentConfigurationTemplate.MessagePipelines = JObject.FromObject(await GetMessagePipelinesAsync(plantId, internalAgentId));
                    return edgeAgentConfigurationTemplate;
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    throw;
                }
            }
        }

        public async Task<EdgeAgentDeploymentPayload> DeployTemplateOnAgentAsync(string plantId, string internalAgentId, EdgeAgentDeploymentPayload edgeAgentPayload)
        {
            using (ILoggerTransaction logger = _loggerTransaction.LogTransaction(GetType().Name))
            {
                try
                {
                    var edgeAgent = await _edgeAgentRepository.GetEdgeAgentByIdAsync(plantId, internalAgentId);
                    EdgeAgentDeploymentPayloadDto edgeAgentPayloadDto = new EdgeAgentDeploymentPayloadDto(edgeAgentPayload);
                    edgeAgentPayloadDto.IsApplyConfigurationContentOnAgent = await _ioTHubRepository.SetDeviceModuleAsync(edgeAgent.AgentId, edgeAgentPayload.DeploymentTemplate.ToString());
                    var edgeAgentDeployedTemplates = await _orchestrateRepository.GetDeployedTemplatesAsync(plantId, internalAgentId);

                    edgeAgent.UpdatedDate = DateTime.UtcNow.ToString();
                    edgeAgent.ManagedBy = Constants.ManagedByEdgeV2;
                    edgeAgent.VisibleOnScreen = Constants.VisibleOnScreenEdgeV2;

                    if (edgeAgentDeployedTemplates == null || !edgeAgentDeployedTemplates.Any())
                    {
                        edgeAgent.StartDate = DateTime.UtcNow.ToString();
                    }

                    await _edgeAgentRepository.UpdateEdgeAgentAsync(edgeAgent.Id, edgeAgent);

                    var deployTemplates = await _orchestrateRepository.GetDeployedTemplatesAsync(plantId, internalAgentId);

                    if (deployTemplates?.Any() == true)
                    {
                        var fileterdeployTemplates = deployTemplates.Where(template => template.IsLatestDeployedTemplate);
                        foreach (var template in fileterdeployTemplates)
                        {
                            template.IsLatestDeployedTemplate = false;
                            await _orchestrateRepository.UpdateDeployedTemplateAsync(template.Id, template);
                        }
                    }

                    if (string.IsNullOrEmpty(edgeAgentPayloadDto.Id))
                    {
                        edgeAgentPayloadDto.IsDeleted = false;
                        edgeAgentPayloadDto.PlantId = plantId;
                        edgeAgentPayloadDto.InternalAgentId = internalAgentId;
                        edgeAgentPayloadDto.IsLatestDeployedTemplate = true;
                        edgeAgentPayloadDto.AgentConfigurationName = string.Format(AgentonfigurationNameTemplate, edgeAgent.AgentName, DateTime.UtcNow.ToString());
                        await _orchestrateRepository.AddDeployedTemplateAsync(edgeAgentPayloadDto);
                    }
                    else
                    {
                        var deployedTemplate = deployTemplates.FirstOrDefault(template => template.Id == edgeAgentPayloadDto.Id);
                        deployedTemplate.UserComments = edgeAgentPayloadDto.UserComments;
                        deployedTemplate.DeploymentConfiguration = edgeAgentPayloadDto.DeploymentConfiguration;
                        deployedTemplate.DeploymentTemplate = edgeAgentPayloadDto.DeploymentTemplate;
                        deployedTemplate.IsLatestDeployedTemplate = true;
                        deployedTemplate.AgentConfigurationName = edgeAgentPayloadDto.AgentConfigurationName;
                        deployedTemplate.IsApplyConfigurationContentOnAgent = edgeAgentPayloadDto.IsApplyConfigurationContentOnAgent;
                        await _orchestrateRepository.UpdateDeployedTemplateAsync(edgeAgentPayloadDto.Id, deployedTemplate);
                    }

                    return new EdgeAgentDeploymentPayload
                    {
                        Id = edgeAgentPayloadDto.Id,
                        IsApplyConfigurationContentOnAgent = edgeAgentPayloadDto.IsApplyConfigurationContentOnAgent,
                        DeploymentTemplate = edgeAgentPayloadDto.DeploymentTemplate,
                        AgentConfigurationName = edgeAgentPayloadDto.AgentConfigurationName,
                        DeploymentConfiguration = edgeAgentPayload.DeploymentConfiguration,
                        UserComments = edgeAgentPayloadDto.UserComments,
                        IsLatestDeployedTemplate = edgeAgentPayloadDto.IsLatestDeployedTemplate
                    };
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    throw;
                }
            }
        }

        public async Task<IEnumerable<EdgeAgentDeploymentPayload>> GetDeployedTemplatesByInternalAgentIdAsync(string plantId, string internalAgentId)
        {
            using (ILoggerTransaction logger = _loggerTransaction.LogTransaction(GetType().Name))
            {
                try
                {
                    var edgeAgentPayloads = await _orchestrateRepository.GetDeployedTemplatesAsync(plantId, internalAgentId);
                    return edgeAgentPayloads;
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    throw;
                }
            }
        }

        public async Task<EdgeAgentDeploymentPayload> GetDeployedTemplateByIdAsync(string plantId, string internalAgentId, string id)
        {
            using (ILoggerTransaction logger = _loggerTransaction.LogTransaction(GetType().Name))
            {
                try
                {
                    var edgeAgentPayload = await _orchestrateRepository.GetDeployedTemplateAsync(plantId, internalAgentId, id);
                    return edgeAgentPayload;
                }
                catch (Exception ex)
                {
                    logger.LogException(ex);
                    throw;
                }
            }
        }

        private async Task<IDictionary<string, JToken>> GetMessagePipelinesAsync(string plantId, string internalAgentId)
        {
            var edgeMessagePipelines = await _edgeMessagePipelineService.GetEdgeMessagePipelinesByAgentIdAsync(plantId, internalAgentId, null);
            IDictionary<string, JToken> edgeMessagePipelineData = new Dictionary<string, JToken>();

            if (edgeMessagePipelines?.Any() == true)
            {
                var modulesOfAgent = await _edgeMessagePipelineRepository.GetEdgeModuleConfigurationsByAgentIdAsync(plantId, internalAgentId);

                if (modulesOfAgent?.Any() == true)
                {
                    foreach (var msg in edgeMessagePipelines)
                    {
                        var adapters = modulesOfAgent.Where(adapter => adapter.MessagePipelineId == msg.Id && adapter.ModuleType == EdgeModuleType.Adapter);
                        var modules = modulesOfAgent.Where(module => module.MessagePipelineId == msg.Id && module.ModuleType == EdgeModuleType.Module);
                        IDictionary<string, JToken> adapterViewData = new Dictionary<string, JToken>();
                        IDictionary<string, JToken> moduleViewData = new Dictionary<string, JToken>();
                        PrepareMessagePipelineAllModuleConfigurations(msg.Modules.Where(mod => mod.Class.ToLower() != "core"), adapters, modules, adapterViewData, moduleViewData);
                        var msgInfo = new
                        {
                            Adapters = JObject.FromObject(adapterViewData),
                            Modules = JObject.FromObject(moduleViewData)
                        };

                        edgeMessagePipelineData.Add(msg.Name, JObject.FromObject(msgInfo));
                    }
                }
            }

            return edgeMessagePipelineData;
        }

        private void PrepareMessagePipelineAllModuleConfigurations(IEnumerable<EdgeModule> messagePipelineModules, IEnumerable<EdgeMessagePipelineModuleDto> adapters, IEnumerable<EdgeMessagePipelineModuleDto> modules, IDictionary<string, JToken> adapterViewData, IDictionary<string, JToken> moduleViewData)
        {
            if (messagePipelineModules?.Any() == true)
            {
                foreach (var module in messagePipelineModules)
                {
                    if (adapters?.Any() == true)
                    {
                        var adaptersOfMessagePipeline = adapters.Where(adp => adp.ModuleImageName == module.ImageName);
                        PrepareMessagePipelineConfigurations(adaptersOfMessagePipeline, adapterViewData, module.ImageName);
                    }

                    if (modules?.Any() == true)
                    {
                        var modulesOfMessagePipeline = modules.Where(adp => adp.ModuleImageName == module.ImageName);
                        PrepareMessagePipelineConfigurations(modulesOfMessagePipeline, moduleViewData, module.ImageName);
                    }
                }
            }
        }

        private void PrepareMessagePipelineConfigurations(IEnumerable<EdgeMessagePipelineModule> modules, IDictionary<string, JToken> moduleViewData, string moduleImageName)
        {
            IDictionary<string, JToken> preparedConfigurationData = new Dictionary<string, JToken>();

            if (modules?.Any() == true)
            {
                foreach (var module in modules)
                {
                    var moduleConfigurtions = new
                    {
                        RootConfiguration = module.RootModuleConfiguration,
                        LageTwinBlobUrl = module.LargeTwinBlobFileURI,
                        ConfigurationFileUrl = module.ModuleConfigurationBlobFileURI
                    };

                    preparedConfigurationData.Add(module.ConfigurationName, JObject.FromObject(moduleConfigurtions));
                }

                moduleViewData.Add(moduleImageName, JObject.FromObject(preparedConfigurationData));
            }
        }

        private async Task<IEnumerable<EdgeMessagePipelineModule>> GetDefaultModulesOfAgentAsync(string plantId, string internalAgentId)
        {
            var defaultModulesOfAgent = await _edgeAgentRepository.GetEdgeAgentDefaultModuleAsync(plantId, internalAgentId);
            IList<EdgeMessagePipelineModule> prparedDefaultModules = new List<EdgeMessagePipelineModule>();

            foreach (var module in defaultModulesOfAgent)
            {
                EdgeMessagePipelineModule messagePipelineModule = new EdgeMessagePipelineModule();
                messagePipelineModule.ConfigurationName = module.ImageName;
                messagePipelineModule.ImageURI = module.ImageURI;
                messagePipelineModule.ModuleName = module.Name;
                messagePipelineModule.ModuleImageName = module.ImageName;
                messagePipelineModule.ModuleTag = module.ModuleVersion;
                prparedDefaultModules.Add(messagePipelineModule);
            }

            return prparedDefaultModules;
        }

        private IEnumerable<EdgeMessagePipelineModule> GetFinalModulesOfAgent(IEnumerable<EdgeMessagePipelineModule> defaultModulesOfAgent, IEnumerable<EdgeMessagePipelineModule> modulesOfAgent)
        {
            var combineDefaultModulesWithAdapters = (defaultModulesOfAgent ?? Enumerable.Empty<EdgeMessagePipelineModule>()).Concat(modulesOfAgent ?? Enumerable.Empty<EdgeMessagePipelineModule>());
            return combineDefaultModulesWithAdapters.ToList();
        }

        private IDictionary<string, DeploymentManifestModule> GetModulesForTemplate(IEnumerable<EdgeMessagePipelineModule> defaultModulesOfAgent, IEnumerable<EdgeMessagePipelineModule> modulesOfAgent, EdgeConfiguration edgeConfiguration)
        {
            var preparedDefaultModules = GetPreaparedDefaultModulesOfAgent(defaultModulesOfAgent, edgeConfiguration);
            var preparedModules = GetPreaparedModulesOfAgent(modulesOfAgent, edgeConfiguration);
            preparedDefaultModules.ToList().ForEach(x => preparedModules.Add(x.Key, x.Value));
            return preparedModules;
        }

        private string GetMaxLogFilesToBeCreatedOnGateway(string logRetentionSpace)
        {
            var totalSize = GetLogRetentionSize(logRetentionSpace);
            var maxFile = totalSize / Convert.ToInt32(_configuration.EdgeModuleMaxFiles);
            return maxFile.ToString();
        }

        private double GetLogRetentionSize(string logRetentionSpace)
        {
            double totalSize = 0;
            var longRetentionSpaceType = logRetentionSpace.Split('-');

            switch (longRetentionSpaceType[1].ToLower())
            {
                case "gb":
                    {
                        totalSize = Convert.ToDouble(longRetentionSpaceType[0]) * 1024;
                        break;
                    }

                case "mb":
                    {
                        totalSize = Convert.ToDouble(longRetentionSpaceType[0]);
                        break;
                    }
            }

            return totalSize;
        }

        private IDictionary<string, string> GetModuleLogConfigurationType(EdgeModuleAdvanceConfiguration advanceConfiguration)
        {
            IDictionary<string, string> logType = new Dictionary<string, string>();
            if (advanceConfiguration != null)
            {
                logType.Add(Constants.MaxSize, string.Format(Constants.MaxSizeTemplate, GetLogRetentionSize(advanceConfiguration.LogRetentionSpace).ToString()));
                logType.Add(Constants.MaxFile, GetMaxLogFilesToBeCreatedOnGateway(advanceConfiguration.LogRetentionSpace));
            }

            return logType;
        }

        private IDictionary<string, DeploymentManifestModule> GetPreaparedDefaultModulesOfAgent(IEnumerable<EdgeMessagePipelineModule> defaultModules, EdgeConfiguration edgeConfiguration)
        {
            var finalModuleList = new Dictionary<string, DeploymentManifestModule>();
            foreach (var edgeModule in defaultModules)
            {
                switch (edgeModule.ModuleImageName)
                {
                    case DefaultUpstreamModule:
                        {
                            var createOptionsJObject = GetCreateOptions().Value;
                            createOptionsJObject[Constants.HostConfig][Constants.LogConfig][Constants.Config].Replace(JObject.FromObject(GetModuleLogConfigurationType(edgeConfiguration.AdvanceConfiguration)));

                            var deploymentManifestModuleSettings = new DeploymentManifestModuleSettings
                            {
                                CreateOptions = createOptionsJObject.ToString(),
                                EdgeModuleContainerImage = edgeModule.ImageURI
                            };

                            var module = new DeploymentManifestModule
                            {
                                Settings = deploymentManifestModuleSettings,
                                EnvironmentVariables = JObject.FromObject(GetLogLevelData(edgeConfiguration.AdvanceConfiguration, edgeConfiguration.IsCompressionEnabled))
                            };

                            finalModuleList.Add(edgeModule.ConfigurationName, module);
                            break;
                        }
                }
            }

            return finalModuleList;
        }

        private IDictionary<string, ModuleEnvironmentValue> GetLogLevelData(EdgeModuleAdvanceConfiguration advanceConfiguration, bool isCompressionEnabled)
        {
            IDictionary<string, ModuleEnvironmentValue> envData = new Dictionary<string, ModuleEnvironmentValue>();
            if (advanceConfiguration != null)
            {
                envData.Add(Constants.ModuleLogLevel, new ModuleEnvironmentValue { Value = advanceConfiguration.LogLevel });
            }

            envData.Add(Constants.ModuleMessageCompression, new ModuleEnvironmentValue { Value = isCompressionEnabled.ToString() });
            return envData;
        }

        private IDictionary<string, DeploymentManifestModule> GetPreaparedModulesOfAgent(IEnumerable<EdgeMessagePipelineModule> modulesOfAgent, EdgeConfiguration edgeConfiguration)
        {
            var finalModuleList = new Dictionary<string, DeploymentManifestModule>();

            foreach (var edgeModule in modulesOfAgent)
            {
                var createOptionsJObject = GetCreateOptions().Value;
                createOptionsJObject[Constants.HostConfig][Constants.LogConfig][Constants.Config].Replace(JObject.FromObject(GetModuleLogConfigurationType(edgeModule.AdvanceConfiguration)));

                var deploymentManifestModuleSettings = new DeploymentManifestModuleSettings
                {
                    CreateOptions = createOptionsJObject.ToString(),
                    EdgeModuleContainerImage = edgeModule.ImageURI
                };

                var module = new DeploymentManifestModule
                {
                    Settings = deploymentManifestModuleSettings,
                    EnvironmentVariables = JObject.FromObject(GetLogLevelData(edgeModule.AdvanceConfiguration, edgeConfiguration.IsCompressionEnabled))
                };
                
                finalModuleList.Add(edgeModule.ConfigurationName, module);
            }

            return finalModuleList;
        }

        private async Task<IDictionary<string, string>> GetRoutesForTemplateAsync(string plantId, string internalAgentId, IEnumerable<EdgeMessagePipelineModule> defaultAgentModules, IEnumerable<EdgeMessagePipelineModule> finalModulesOfAgent)
        {
            var routes = await _edgeMessagePipelineRepository.GetEdgeRoutesByInternalAgentIdAsync(plantId, internalAgentId);
            IDictionary<string, string> routesForTemplate = new Dictionary<string, string>();
            PeparedDefaultModulesRoutes(defaultAgentModules, routesForTemplate);
            foreach (var route in routes)
            {
                var sourceModulesOfRoute = finalModulesOfAgent.Where(module => module.ModuleImageName.ToLower() == route.SourceModule.ModuleImage.ToLower());
                PreapareRouteForSourceModules(route, finalModulesOfAgent, sourceModulesOfRoute, routesForTemplate);
            }

            return routesForTemplate;
        }

        private void PeparedDefaultModulesRoutes(IEnumerable<EdgeMessagePipelineModule> defaultAgentModules, IDictionary<string, string> routesForTemplate)
        {
            foreach (var edgeModule in defaultAgentModules)
            {
                switch (edgeModule.ModuleImageName)
                {
                    case DefaultUpstreamModule:
                        {
                            routesForTemplate.Add(Constants.UpstreamToIoTHub, string.Format(Constants.UpstreamTemplate, edgeModule.ModuleImageName, Constants.Upstream));
                            break;
                        }
                }
            }
        }

        private void PreapareRouteForSourceModules(EdgeRoute route, IEnumerable<EdgeMessagePipelineModule> finalModulesOfAgent, IEnumerable<EdgeMessagePipelineModule> sourceModulesOfRoute, IDictionary<string, string> routesForTemplate)
        {
            foreach (var sourceModuleOfRoute in sourceModulesOfRoute)
            {
                PreapareRouteForTargetModules(route.TargetModules, finalModulesOfAgent, sourceModuleOfRoute, routesForTemplate);
            }
        }

        private void PreapareRouteForTargetModules(IEnumerable<EdgeRouteModule> targetModulesOfRoute, IEnumerable<EdgeMessagePipelineModule> finalModulesOfAgent, EdgeMessagePipelineModule sourceModuleOfRoute, IDictionary<string, string> routesForTemplate)
        {
            foreach (var targetRouteModule in targetModulesOfRoute)
            {
                var targetModules = finalModulesOfAgent.Where(module => module.ModuleImageName.ToLower() == targetRouteModule.ModuleImage.ToLower());
                PreapareRoutesOfModules(targetModules, sourceModuleOfRoute, routesForTemplate);
            }
        }

        private void PreapareRoutesOfModules(IEnumerable<EdgeMessagePipelineModule> targetModules, EdgeMessagePipelineModule sourceModuleOfRoute, IDictionary<string, string> routesForTemplate)
        {
            foreach (var targetModule in targetModules)
            {
                string key = string.Format(Constants.RouteNameTemplate, Regex.Replace(sourceModuleOfRoute.ConfigurationName, @"[^0-9a-zA-Z]+", string.Empty), Regex.Replace(targetModule.ConfigurationName, @"[^0-9a-zA-Z]+", string.Empty));

                if (!routesForTemplate.ContainsKey(key))
                {
                    routesForTemplate.Add(key, string.Format(Constants.RouteTemplate, sourceModuleOfRoute.ModuleName, targetModule.ModuleName));
                }
            }
        }

        private void SetModuleDesiredProperties(IEnumerable<EdgeMessagePipelineModule> moduleList, JTokenWrapper deploymentJsonObject, EdgeAgentDto edgeAgent, EdgeConfiguration edgeConfiguration)
        {
            foreach (var edgeModule in moduleList)
            {
                var moduleDesiredProperty = new JObject();
                switch (edgeModule.ModuleImageName)
                {
                    case DefaultUpstreamModule:
                        {
                            var desiredProperties = new
                            {
                                configuration = new
                                { }
                            };

                            moduleDesiredProperty[Constants.DesiredProperties] = JObject.FromObject(desiredProperties);
                            deploymentJsonObject.Value[edgeModule.ConfigurationName] = moduleDesiredProperty;
                            break;
                        }

                    default:
                        {
                            var desiredProperties = new
                            {
                                configuration = new
                                {
                                    timeStamp = DateTime.UtcNow.ToString("o"),
                                    blobSas = string.IsNullOrEmpty(edgeModule.ModuleConfigurationBlobFileURI) ? string.Empty : edgeModule.ModuleConfigurationBlobFileURI
                                }
                            };

                            moduleDesiredProperty[Constants.DesiredProperties] = JObject.FromObject(desiredProperties);
                            deploymentJsonObject.Value[edgeModule.ConfigurationName] = moduleDesiredProperty;
                            break;
                        }
                }
            }
        }

        private void SetRegistryCredentials(JTokenWrapper deploymentJsonObject)
        {
            IDictionary<string, Dictionary<string, string>> registryCredentials = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> userDetails = new Dictionary<string, string>();
            userDetails.Add(_configuration.AcrUserNameTemplate, _configuration.AcrUserName);
            userDetails.Add(_configuration.AcrUserPasswordTemplate, _configuration.AcrPassword);
            userDetails.Add(Constants.UserAddress, _configuration.AcrRepositoryEndpoint);
            registryCredentials.Add(_configuration.AcrUserName, userDetails);
            var registryCredentialsToken = GetEdgeAgentDesiredPropertiesRegistryCredentials(deploymentJsonObject).Value;
            registryCredentialsToken.Replace(JObject.FromObject(registryCredentials));
        }

        private void UpdateEdgeHub(JTokenWrapper deploymentJsonObject, EdgeConfiguration edgeConfiguration, EdgeGatewayConfiguration edgeGatewayConfiguration)
        {
            var createOptionsToken = GetEdgeAgentDesiredPropertiesEdgeHubCreateOption(deploymentJsonObject);
            UpdateSystemModuleCreateOptions(createOptionsToken, edgeConfiguration, edgeGatewayConfiguration);

            var messageTTLToken = GetEdgeHubDesiredPropertiesMessageTTL(deploymentJsonObject).Value;
            messageTTLToken.Replace(Convert.ToInt32(edgeConfiguration.MessageTTL));
            var edgeHub = GetEdgeAgentDesiredPropertiesEdgeHubModule(deploymentJsonObject);
            SetHttpProxyWithUpstreamProtocolDetails(edgeHub, edgeGatewayConfiguration);
        }
        
        private void UpdateEdgeAgent(JTokenWrapper deploymentJsonObject, EdgeConfiguration edgeConfiguration, EdgeGatewayConfiguration edgeGatewayConfiguration)
        {
            var createOptionsToken = GetEdgeAgentDesiredPropertiesEdgeAgentCreateOption(deploymentJsonObject);
            UpdateSystemModuleCreateOptions(createOptionsToken, edgeConfiguration, edgeGatewayConfiguration);

            var edgeAgent = GetEdgeAgentDesiredPropertiesEdgeAgentModule(deploymentJsonObject);
            SetHttpProxyWithUpstreamProtocolDetails(edgeAgent, edgeGatewayConfiguration);
        }

        private void UpdateSystemModuleCreateOptions(JTokenWrapper createOptionsToken, EdgeConfiguration edgeConfiguration, EdgeGatewayConfiguration edgeGatewayConfiguration)
        {
            string[] communicationPort = edgeConfiguration.UpstreamCommunication.Split('/');
            IDictionary<string, Dictionary<string, string>[]> edgeHubPortBindingsConfig = new Dictionary<string, Dictionary<string, string>[]>();
            Dictionary<string, string>[] matrix = new Dictionary<string, string>[1];
            matrix[0] = new Dictionary<string, string>();
            matrix[0].Add(Constants.HostPort, communicationPort[0]);
            edgeHubPortBindingsConfig.Add(edgeConfiguration.UpstreamCommunication, matrix);

            var createOption = new
            {
                HostConfig = new
                {
                    PortBindings = JObject.FromObject(edgeHubPortBindingsConfig),
                    LogConfig = new
                    {
                        Type = "json-file",
                        Config = JObject.FromObject(GetModuleLogConfigurationType(edgeConfiguration.AdvanceConfiguration))
                    }
                }
            };

            createOptionsToken.Value.Replace(JObject.FromObject(createOption).ToString());
        }

        private void SetHttpProxyWithUpstreamProtocolDetails(JTokenWrapper module, EdgeGatewayConfiguration edgeGatewayConfiguration)
        {
            if (edgeGatewayConfiguration.ProxyUser != null && edgeGatewayConfiguration.ProxyHost != null && edgeGatewayConfiguration.ProxyPassword != null)
            {
                IDictionary<string, ModuleEnvironmentValue> envData = new Dictionary<string, ModuleEnvironmentValue>();
                var hostProxyDetails = string.Format(Constants.ProxyTemplate, edgeGatewayConfiguration.ProxyUser, edgeGatewayConfiguration.ProxyPassword, edgeGatewayConfiguration.ProxyHost);
                envData.Add(Constants.HttpsProxy, new ModuleEnvironmentValue { Value = hostProxyDetails });
                envData.Add(Constants.UpstreamProtocl, new ModuleEnvironmentValue { Value = Constants.UpstreamProtocolValue });
                module.Value[Constants.ModuleEnv] = JObject.FromObject(envData);
            }
        }

        private JTokenWrapper GetEdgeAgentDesiredProperties(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            jtokenWrapper.Value = deploymentJsonObject.Value[Constants.EdgeAgent][Constants.DesiredProperties];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesModules(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredProperties = GetEdgeAgentDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredProperties[Constants.Modules];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesSystemModules(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredProperties = GetEdgeAgentDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredProperties[Constants.SystemModules];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeHubDesiredProperties(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            jtokenWrapper.Value = deploymentJsonObject.Value[Constants.EdgeHub][Constants.DesiredProperties];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeHubDesiredPropertiesRoutes(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeHubDesiredPropertie = GetEdgeHubDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeHubDesiredPropertie[Constants.Routes];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesEdgeAgentCreateOption(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredPropeties = GetEdgeAgentDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredPropeties[Constants.SystemModules][Constants.SystemModulesEdgeAgent][Constants.Settings][Constants.CreateOptions];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesEdgeHubCreateOption(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredPropeties = GetEdgeAgentDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredPropeties[Constants.SystemModules][Constants.SystemModulesEdgeHub][Constants.Settings][Constants.CreateOptions];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesEdgeAgentModule(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredPropeties = GetEdgeAgentDesiredPropertiesSystemModules(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredPropeties[Constants.SystemModulesEdgeAgent];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesEdgeHubModule(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredPropeties = GetEdgeAgentDesiredPropertiesSystemModules(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredPropeties[Constants.SystemModulesEdgeHub];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeAgentDesiredPropertiesRegistryCredentials(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeAgentDesiredPropeties = GetEdgeAgentDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeAgentDesiredPropeties[Constants.Runtime][Constants.Settings][Constants.RegistryCredentials];
            return jtokenWrapper;
        }

        private JTokenWrapper GetEdgeHubDesiredPropertiesMessageTTL(JTokenWrapper deploymentJsonObject)
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var edgeHubDesiredPropeties = GetEdgeHubDesiredProperties(deploymentJsonObject).Value;
            jtokenWrapper.Value = edgeHubDesiredPropeties[Constants.StoreForwardConfiguration][Constants.TimeToLiveSecs];
            return jtokenWrapper;
        }

        private JTokenWrapper GetCreateOptions()
        {
            JTokenWrapper jtokenWrapper = new JTokenWrapper();
            var createOptions = new
            {
                HostConfig = new
                {
                    LogConfig = new
                    {
                        Type = "json-file",
                        Config = new JObject()
                    }
                }
            };

            jtokenWrapper.Value = JObject.FromObject(createOptions);
            return jtokenWrapper;
        }
    }
}
