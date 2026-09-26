using IntelligentDocAnalyzer.Models;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace IntelligentDocAnalyzer.DbContext;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<RecognitionJob> RecognitionJobs =>
        _database.GetCollection<RecognitionJob>("recognitionJobs");

    public IGridFSBucket GridFS =>
        new GridFSBucket(_database);
}
