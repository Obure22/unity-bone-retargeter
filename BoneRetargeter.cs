using UnityEngine;
using System.Collections.Generic;

public class BoneRetargeter : MonoBehaviour
{
    // Сюда кидай Hips НОВОГО скелета
    public Transform targetArmature;

    [ContextMenu("Link Existing Bones")]
    public void LinkBones()
    {
        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        if (smr == null || targetArmature == null)
        {
            Debug.LogError("Не назначен SMR или Target Armature!");
            return;
        }

        // 1. Собираем все кости нового скелета в базу
        Dictionary<string, Transform> newBonesMap = new Dictionary<string, Transform>();
        foreach (var bone in targetArmature.GetComponentsInChildren<Transform>(true))
        {
            if (!newBonesMap.ContainsKey(bone.name))
                newBonesMap.Add(bone.name, bone);
        }

        Transform[] currentBones = smr.bones;
        Transform[] newBones = new Transform[currentBones.Length];
        
        int foundCount = 0;
        int missingCount = 0;
        int copiedCount = 0;

        for (int i = 0; i < currentBones.Length; i++)
        {
            if (currentBones[i] == null) continue; 
            
            string boneName = currentBones[i].name; 

            if (newBonesMap.ContainsKey(boneName))
            {
                Transform targetBone = newBonesMap[boneName];
                newBones[i] = targetBone;
                foundCount++;
                
                // Обновляем свойства и компоненты существующей кости
                UpdateBoneProperties(currentBones[i], targetBone);
            }
            else
            {
                // Если кость не найдена — копируем её из старого скелета в новый
                Transform sourceBone = currentBones[i];
                Transform parentBone = FindParentBoneInTarget(sourceBone.parent, newBonesMap, targetArmature);
                
                // Создаем новую кость в целевом скелете
                GameObject newBoneObject = new GameObject(boneName);
                newBoneObject.transform.SetParent(parentBone, false);
                newBoneObject.transform.localPosition = sourceBone.localPosition;
                newBoneObject.transform.localRotation = sourceBone.localRotation;
                newBoneObject.transform.localScale = sourceBone.localScale;
                
                // Копируем все компоненты с исходной кости
                CopyComponents(sourceBone, newBoneObject.transform);
                
                // Добавляем новую кость в карту
                newBonesMap.Add(boneName, newBoneObject.transform);
                newBones[i] = newBoneObject.transform;
                copiedCount++;
                missingCount++;
            }
        }

        smr.bones = newBones; 
        
        // Обновляем Root Bone
        if (smr.rootBone != null && newBonesMap.ContainsKey(smr.rootBone.name))
        {
            smr.rootBone = newBonesMap[smr.rootBone.name];
        }
        
        smr.updateWhenOffscreen = true; // Исправляет мигание
        
        Debug.Log($"ГОТОВО! Привязано: {foundCount}. НЕ НАЙДЕНО: {missingCount}. СКОПИРОВАНО: {copiedCount}.");
    }

    private Transform FindParentBoneInTarget(Transform parentBone, Dictionary<string, Transform> newBonesMap, Transform targetArmature)
    {
        if (parentBone == null)
            return targetArmature;
        
        if (newBonesMap.ContainsKey(parentBone.name))
            return newBonesMap[parentBone.name];
        
        // Если родительская кость не найдена, рекурсивно ищем её
        Transform parentInTarget = FindParentBoneInTarget(parentBone.parent, newBonesMap, targetArmature);
        
        // Создаем родительскую кость, если её нет
        GameObject newParentBoneObject = new GameObject(parentBone.name);
        newParentBoneObject.transform.SetParent(parentInTarget, false);
        newParentBoneObject.transform.localPosition = parentBone.localPosition;
        newParentBoneObject.transform.localRotation = parentBone.localRotation;
        newParentBoneObject.transform.localScale = parentBone.localScale;
        
        // Копируем все компоненты с исходной родительской кости
        CopyComponents(parentBone, newParentBoneObject.transform);
        
        // Добавляем новую родительскую кость в карту
        newBonesMap.Add(parentBone.name, newParentBoneObject.transform);
        
        return newParentBoneObject.transform;
    }

    private void UpdateBoneProperties(Transform source, Transform target)
    {
        // Обновляем трансформацию кости
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        
        // Копируем все компоненты с исходной кости
        CopyComponents(source, target);
    }

    private void CopyComponents(Transform source, Transform target)
    {
        // Копируем все компоненты с исходной кости
        Component[] components = source.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component is Transform) continue; // Пропускаем Transform, так как он уже скопирован
            
            // Проверяем, есть ли уже такой компонент на целевой кости
            Component existingComponent = target.GetComponent(component.GetType());
            if (existingComponent != null)
            {
                // Если компонент уже есть, обновляем его свойства
                System.Reflection.FieldInfo[] fields = component.GetType().GetFields();
                foreach (System.Reflection.FieldInfo field in fields)
                {
                    field.SetValue(existingComponent, field.GetValue(component));
                }
            }
            else
            {
                // Если компонента нет, добавляем его
                Component newComponent = target.gameObject.AddComponent(component.GetType());
                
                // Копируем свойства компонента
                System.Reflection.FieldInfo[] fields = component.GetType().GetFields();
                foreach (System.Reflection.FieldInfo field in fields)
                {
                    field.SetValue(newComponent, field.GetValue(component));
                }
            }
        }
    }
}