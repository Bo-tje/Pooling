#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "UnrealObjectPooler.generated.h"

/**
 * Enum defining the different types of pools available.
 * Used to organize pooled objects under specific parent actors/folders.
 */
UENUM(BlueprintType)
enum class EPoolType : uint8
{
	Nodes,
	NodesSpawner,
	GameObjects,
	Billboards
};

/**
 * Internal struct to manage a single pool of actors
 */
USTRUCT()
struct FActorPool
{
	GENERATED_BODY()

	UPROPERTY()
	TArray<AActor*> InactiveActors;

	UPROPERTY()
	AActor* PoolRoot = nullptr;
};

/**
 * A World Subsystem that manages object pooling in Unreal Engine.
 * This is the Unreal equivalent of the Unity ObjectPooler script.
 */
UCLASS()
class POOLING_API UUnrealObjectPooler : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	// Get the subsystem instance
	static UUnrealObjectPooler* Get(const UObject* WorldContextObject);

	/**
	 * Spawns (or retrieves from pool) an actor of the specified class.
	 */
	UFUNCTION(BlueprintCallable, Category = "Pooling", meta = (WorldContext = "WorldContextObject", DeterminesOutputType = "ActorClass"))
	AActor* SpawnObject(TSubclassOf<AActor> ActorClass, FVector Location, FRotator Rotation, EPoolType PoolType = EPoolType::GameObjects);

	/**
	 * Template version of SpawnObject for C++ convenience
	 */
	template<class T>
	T* SpawnObject(TSubclassOf<T> ActorClass, FVector Location, FRotator Rotation, EPoolType PoolType = EPoolType::GameObjects)
	{
		return Cast<T>(SpawnObject(TSubclassOf<AActor>(*ActorClass), Location, Rotation, PoolType));
	}

	/**
	 * Returns an actor to its corresponding pool.
	 */
	UFUNCTION(BlueprintCallable, Category = "Pooling")
	void ReturnObjectToPool(AActor* Actor);

	/**
	 * Pre-warms a pool by creating a specified number of actors.
	 */
	UFUNCTION(BlueprintCallable, Category = "Pooling")
	void InitializePool(TSubclassOf<AActor> ActorClass, int32 Count, EPoolType PoolType = EPoolType::GameObjects);

protected:
	// Subsystem lifecycle
	virtual void Deinitialize() override;

private:
	// Map of Actor Classes to their respective pools
	UPROPERTY()
	TMap<UClass*, FActorPool> ObjectPools;

	// Map to track which class a spawned actor belongs to
	UPROPERTY()
	TMap<AActor*, UClass*> ActorToClassMap;

	// Dictionary to store root actors for each pool type
	UPROPERTY()
	TMap<EPoolType, AActor*> PoolRoots;

	// Main root actor for all pools
	UPROPERTY()
	AActor* MainPoolRoot = nullptr;

	AActor* GetOrCreatePoolRoot(EPoolType PoolType);
	void SetActorActive(AActor* Actor, bool bActive);
};
