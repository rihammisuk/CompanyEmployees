using Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository
{
    public sealed class RepositoryManager : IRepositoryManager
    {
        private readonly RepositoryContext _repositoryContext;
        private readonly Lazy<ICompanyRepository> _companyRepository;
        private readonly Lazy<IEmployeeRepository> _employeeRepository;

        public RepositoryManager(RepositoryContext repositoryContext)
        {
            _repositoryContext = repositoryContext;

            //  Lazy<T> This means that our repository instances are only going to be created when we access them for the first time, and not before that.
            _companyRepository = new Lazy<ICompanyRepository>(()=> new CompanyRepository(repositoryContext));
            _employeeRepository = new Lazy<IEmployeeRepository>(()=> new EmployeeRepository(repositoryContext));
        }

        public ICompanyRepository Company => _companyRepository.Value;
        public IEmployeeRepository Employee => _employeeRepository.Value;

        public void Save() => _repositoryContext.SaveChanges();
    }
}
