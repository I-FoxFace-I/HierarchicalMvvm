using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace HierarchicalMvvm.Generator;

public static class TopologicalSorter
{
    public static List<ModelGenerationInfo> SortByDependencies(ModelGenerationInfo[] modelInfos)
    {
        var graph = new Dictionary<ModelGenerationInfo, List<ModelGenerationInfo>>();
        var inDegree = new Dictionary<ModelGenerationInfo, int>();
        var typeToModelMap = new Dictionary<string, ModelGenerationInfo>();
        
        // Inicializace grafu a mapy typů
        foreach (var model in modelInfos)
        {
            graph[model] = new List<ModelGenerationInfo>();
            inDegree[model] = 0;
            typeToModelMap[model.TargetWrapperType.Name] = model;
        }
        
        // Vytvoření hran podle závislostí
        foreach (var model in modelInfos)
        {
            var dependencies = GetDependencies(model, typeToModelMap);
            foreach (var dep in dependencies)
            {
                // dep musí být generován před model
                graph[dep].Add(model);
                inDegree[model]++;
            }
        }
        
        // Topologický sort pomocí Kahn's algoritmu
        var result = new List<ModelGenerationInfo>();
        var queue = new Queue<ModelGenerationInfo>();
        
        // Najdi všechny uzly bez incoming edges
        foreach (var model in modelInfos)
        {
            if (inDegree[model] == 0)
                queue.Enqueue(model);
        }
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            
            // Pro každý soused aktuálního uzlu
            foreach (var neighbor in graph[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }
        
        // Zkontroluj, zda neexistují cyklické závislosti
        if (result.Count != modelInfos.Length)
        {
            // Cyklické závislosti - vrať původní pořadí
            return modelInfos.ToList();
        }
        
        return result;
    }
    
    private static List<ModelGenerationInfo> GetDependencies(ModelGenerationInfo model, Dictionary<string, ModelGenerationInfo> typeToModelMap)
    {
        var dependencies = new List<ModelGenerationInfo>();
        
        // Získej properties pro analýzu závislostí
        var properties = PropertyAnalyzer.GetProperties(model.TargetWrapperType, new Dictionary<string, string>());
        var dependentTypes = PropertyAnalyzer.GetDependentTypes(properties);
        
        foreach (var dependentType in dependentTypes)
        {
            // Pokud existuje model pro tento typ, přidej závislost
            if (typeToModelMap.TryGetValue(dependentType.Name, out var dependentModel))
            {
                dependencies.Add(dependentModel);
            }
        }
        
        // Přidej závislost na base typ, pokud existuje
        if (model.BaseWrapperType != null && typeToModelMap.TryGetValue(model.BaseWrapperType.Name, out var baseModel))
        {
            dependencies.Add(baseModel);
        }
        
        return dependencies;
    }
    
    public static bool HasCyclicDependencies(ModelGenerationInfo[] modelInfos)
    {
        var sorted = SortByDependencies(modelInfos);
        return sorted.Count != modelInfos.Length;
    }
} 