#include "UnrealObjectPooler.h"
#include "Kismet/GameplayStatics.h"
#include "GameFramework/Actor.h"

UUnrealObjectPooler* UUnrealObjectPooler::Get(const UObject* WorldContextObject)
{
	if (UWorld* World = GEngine->GetWorldFromContextObject(WorldContextObject, EGetWorldErrorMode::LogAndReturnNull))
	{
		return World->GetSubsystem<UUnrealObjectPooler>();
	}
	return nullptr;
}

AActor* UUnrealObjectPooler::SpawnObject(TSubclassOf<AActor> ActorClass, FVector Location, FRotator Rotation, EPoolType PoolType)
{
	if (!ActorClass) return nullptr;

	UWorld* World = GetWorld();
	if (!World) return nullptr;

	FActorPool& Pool = ObjectPools.FindOrAdd(ActorClass);
	AActor* SpawnedActor = nullptr;

	// Use existing actor if available
	while (Pool.InactiveActors.Num() > 0)
	{
		SpawnedActor = Pool.InactiveActors.Pop();
		if (IsValid(SpawnedActor))
		{
			break;
		}
		SpawnedActor = nullptr; // Cleanup invalid actors
	}

	// Create new actor if none available in pool
	if (!SpawnedActor)
	{
		FActorSpawnParameters SpawnParams;
		SpawnParams.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		
		SpawnedActor = World->SpawnActor<AActor>(ActorClass, Location, Rotation, SpawnParams);
		
		if (SpawnedActor)
		{
			ActorToClassMap.Add(SpawnedActor, ActorClass);
			
			// Parent to pool root
			AActor* Root = GetOrCreatePoolRoot(PoolType);
			SpawnedActor->AttachToActor(Root, FAttachmentTransformRules::KeepWorldTransform);
		}
	}

	if (SpawnedActor)
	{
		SpawnedActor->SetActorLocationAndRotation(Location, Rotation);
		SetActorActive(SpawnedActor, true);
	}

	return SpawnedActor;
}

void UUnrealObjectPooler::ReturnObjectToPool(AActor* Actor)
{
	if (!IsValid(Actor)) return;

	UClass** ClassPtr = ActorToClassMap.Find(Actor);
	if (ClassPtr && *ClassPtr)
	{
		SetActorActive(Actor, false);
		
		FActorPool& Pool = ObjectPools.FindOrAdd(*ClassPtr);
		Pool.InactiveActors.AddUnique(Actor);

		// Optionally re-attach to pool root if it was moved
		// We can determine the original PoolType if we stored it, 
		// but for simplicity we'll just keep it where it is or re-parent to a general root.
	}
	else
	{
		UE_LOG(LogTemp, Warning, TEXT("Trying to return an actor that is not pooled: %s"), *Actor->GetName());
		Actor->Destroy(); // Or just leave it if it shouldn't be handled by us
	}
}

void UUnrealObjectPooler::InitializePool(TSubclassOf<AActor> ActorClass, int32 Count, EPoolType PoolType)
{
	if (!ActorClass) return;

	for (int32 i = 0; i < Count; ++i)
	{
		AActor* NewActor = SpawnObject(ActorClass, FVector::ZeroVector, FRotator::ZeroRotator, PoolType);
		if (NewActor)
		{
			ReturnObjectToPool(NewActor);
		}
	}
}

void UUnrealObjectPooler::Deinitialize()
{
	// Cleanup pools
	for (auto& Pair : ObjectPools)
	{
		for (AActor* Actor : Pair.Value.InactiveActors)
		{
			if (IsValid(Actor))
			{
				Actor->Destroy();
			}
		}
	}
	
	ObjectPools.Empty();
	ActorToClassMap.Empty();
	PoolRoots.Empty();

	if (IsValid(MainPoolRoot))
	{
		MainPoolRoot->Destroy();
		MainPoolRoot = nullptr;
	}

	Super::Deinitialize();
}

AActor* UUnrealObjectPooler::GetOrCreatePoolRoot(EPoolType PoolType)
{
	UWorld* World = GetWorld();
	if (!World) return nullptr;

	if (!IsValid(MainPoolRoot))
	{
		MainPoolRoot = World->SpawnActor<AActor>(AActor::StaticClass());
		MainPoolRoot->SetActorLabel(TEXT("ObjectPools_Root"));
	}

	AActor** RootPtr = PoolRoots.Find(PoolType);
	if (RootPtr && IsValid(*RootPtr))
	{
		return *RootPtr;
	}

	// Create new root for this type
	FActorSpawnParameters SpawnParams;
	AActor* NewRoot = World->SpawnActor<AActor>(AActor::StaticClass(), SpawnParams);
	
	FString EnumName = StaticEnum<EPoolType>()->GetNameStringByValue((int64)PoolType);
	NewRoot->SetActorLabel(FString::Printf(TEXT("PoolRoot_%s"), *EnumName));
	NewRoot->AttachToActor(MainPoolRoot, FAttachmentTransformRules::KeepRelativeTransform);

	PoolRoots.Add(PoolType, NewRoot);
	return NewRoot;
}

void UUnrealObjectPooler::SetActorActive(AActor* Actor, bool bActive)
{
	if (!IsValid(Actor)) return;

	Actor->SetActorHiddenInGame(!bActive);
	Actor->SetActorEnableCollision(bActive);
	Actor->SetActorTickEnabled(bActive);

	// Also handle child components recursively if needed, 
	// but standard AActor functions usually handle the basics.
	// For example, if it has a movement component:
	TArray<UActorComponent*> Components;
	Actor->GetComponents(Components);
	for (UActorComponent* Comp : Components)
	{
		if (bActive)
		{
			Comp->Activate();
		}
		else
		{
			Comp->Deactivate();
		}
	}
}
