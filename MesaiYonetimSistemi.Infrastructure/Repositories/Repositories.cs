using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MesaiYonetimSistemi.Core.Entities;
using MesaiYonetimSistemi.Core.Interfaces;
using MesaiYonetimSistemi.Infrastructure.Data;

namespace MesaiYonetimSistemi.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.SingleOrDefaultAsync(predicate);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            if (predicate == null)
                return await _dbSet.CountAsync();
            return await _dbSet.CountAsync(predicate);
        }
    }

    public class OvertimeRepository : Repository<Overtime>, IOvertimeRepository
    {
        public OvertimeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Overtime>> GetOvertimesWithOffersAsync()
        {
            return await _context.Overtimes
                .Include(o => o.AtananUser)
                .Include(o => o.Offers)
                    .ThenInclude(of => of.Personnel)
                .OrderByDescending(o => o.Tarih)
                .ToListAsync();
        }

        public async Task<Overtime?> GetOvertimeDetailsAsync(int overtimeId)
        {
            return await _context.Overtimes
                .Include(o => o.AtananUser)
                .Include(o => o.Offers)
                    .ThenInclude(of => of.Personnel)
                .Include(o => o.SwapRequests)
                    .ThenInclude(s => s.IstekYapanUser)
                .FirstOrDefaultAsync(o => o.Id == overtimeId);
        }
    }

    public class QueueRepository : Repository<QueueItem>, IQueueRepository
    {
        public QueueRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<QueueItem>> GetOrderedQueueAsync(string? department = null, string? sube = null)
        {
            var query = _context.QueueItems
                .Include(q => q.Personnel)
                .Where(q => q.AktifMi && q.Personnel != null && q.Personnel.AktifMi);

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(q => q.Personnel!.Departman == department);

            if (!string.IsNullOrWhiteSpace(sube))
                query = query.Where(q => q.Personnel!.Sube == sube);

            return await query.OrderBy(q => q.SiraPozisyonu).ToListAsync();
        }

        public async Task<QueueItem?> GetQueueByPersonnelIdAsync(string personnelId)
        {
            return await _context.QueueItems
                .Include(q => q.Personnel)
                .FirstOrDefaultAsync(q => q.PersonnelId == personnelId);
        }

        public async Task ReorderQueueAsync()
        {
            var items = await _context.QueueItems
                .OrderBy(q => q.SiraPozisyonu)
                .ToListAsync();

            for (int i = 0; i < items.Count; i++)
            {
                items[i].SiraPozisyonu = i + 1;
            }
        }
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Users = new Repository<ApplicationUser>(_context);
            Overtimes = new OvertimeRepository(_context);
            OvertimeOffers = new Repository<OvertimeOffer>(_context);
            Queue = new QueueRepository(_context);
            Notifications = new Repository<Notification>(_context);
            Announcements = new Repository<Announcement>(_context);
            OvertimeSwapRequests = new Repository<OvertimeSwapRequest>(_context);
            PersonnelLeaves = new Repository<PersonnelLeave>(_context);
        }

        public IRepository<ApplicationUser> Users { get; private set; }
        public IOvertimeRepository Overtimes { get; private set; }
        public IRepository<OvertimeOffer> OvertimeOffers { get; private set; }
        public IQueueRepository Queue { get; private set; }
        public IRepository<Notification> Notifications { get; private set; }
        public IRepository<Announcement> Announcements { get; private set; }
        public IRepository<OvertimeSwapRequest> OvertimeSwapRequests { get; private set; }
        public IRepository<PersonnelLeave> PersonnelLeaves { get; private set; }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
