namespace Fitbit.Elasticsearch
{
    class Program
    {
        static void Main(string[] args)
        {
            new FitbitToElasticsearch(args[0], args[1]).Dump();
        }
    }
}
