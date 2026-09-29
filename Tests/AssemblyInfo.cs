using Xunit;

// Les générateurs partagent un verrou (GridGenerator.Sync, caches statiques de ConcentricGenerator) :
// en parallèle, les tests de rapidité mesuraient aussi l'attente du verrou tenu par une autre classe.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
