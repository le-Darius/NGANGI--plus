using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

public sealed record DashboardSummary(
    string GroupName, string CycleLabel, long Cash, int ActiveLoanCount, long InterestReceived,
    int PendingFineCount, long PendingFineTotal, MeetingSession? NextSession, int NextSessionPaid, int MemberCount);

public sealed record MemberShare(Member Member, long Contributed, long Gains, long Total);

public sealed record CycleReport(
    long Contributions, long Interest, long Fines, long TotalToShare,
    IReadOnlyList<MemberShare> Shares, bool IsProjection);

/// <summary>
/// Toutes les règles du njangi : cotisations, prêts, intérêts, amendes de retard et bilan.
/// Ne dépend d'aucune interface : on peut donc la tester seule.
/// </summary>
public sealed class NjangiService
{
    /// <summary>Tentatives de connexion échouées avant blocage temporaire d'un numéro.</summary>
    public const int MaxLoginAttempts = 5;
    public static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(5);

    private readonly Func<DateTime> _clock;
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, (int Failures, DateTime LockedUntilUtc)> _loginAttempts = new();

    /// <param name="clock">Date du jour (date seule) : sert aux échéances et aux amendes.</param>
    /// <param name="utcNow">Heure réelle : sert uniquement au blocage temporaire après des échecs de connexion.</param>
    public NjangiService(NjangiData data, Func<DateTime>? clock = null, Func<DateTime>? utcNow = null)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        _clock = clock ?? (() => DateTime.Today);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public NjangiData Data { get; private set; }
    public DateTime Today => _clock().Date;
    public UserAccount? CurrentUser { get; private set; }

    /// <summary>Déclenché après chaque modification (pour sauvegarder).</summary>
    public event Action? Changed;

    private void Touch() => Changed?.Invoke();

    private static int NextId<T>(IEnumerable<T> items, Func<T, int> id) =>
        items.Any() ? items.Max(id) + 1 : 1;

    // ------------------------------------------------------------------ Compte

    /// <summary>Vrai tant que rien n'a été enregistré : ni compte, ni membre (premier lancement).</summary>
    public bool IsEmpty => Data.Accounts.Count == 0 && Data.Members.Count == 0;

    public Member? CurrentMember =>
        CurrentUser == null ? null : Data.Members.FirstOrDefault(m => m.Id == CurrentUser.MemberId);

    public bool CanManage => CurrentMember is { Role: Role.President or Role.Tresorier };

    public bool IsPresident => CurrentMember is { Role: Role.President };

    public OperationResult Register(string fullName, string phone, string password, string confirm, bool acceptedTerms)
    {
        var errors = new List<string>();
        void Add(string? e) { if (e != null) errors.Add(e); }
        Add(Validation.NameError(fullName));
        Add(Validation.PhoneError(phone));
        Add(Validation.PasswordError(password));
        Add(Validation.ConfirmError(password, confirm));
        if (!acceptedTerms) errors.Add("Acceptez les conditions d'utilisation.");
        if (errors.Count > 0) return OperationResult.Fail(errors.ToArray());

        var p = Validation.NormalizePhone(phone)!;
        if (Data.Accounts.Any(a => a.Phone == p))
            return OperationResult.Fail("Un compte existe déjà avec ce numéro.");

        // Base vide : la première personne inscrite crée le groupe et en devient présidente.
        // (Ce groupe provisoire sera remplacé par un assistant de création du njangi.)
        var member = IsEmpty
            ? CreateFounder(Validation.CleanName(fullName), p)
            : Data.Members.FirstOrDefault(m => m.Phone == p) ?? CreateMember(Validation.CleanName(fullName), p);
        var (hash, salt) = Security.HashPassword(password);
        var account = new UserAccount { Phone = p, FullName = member.FullName, PasswordHash = hash, Salt = salt, MemberId = member.Id };
        Data.Accounts.Add(account);
        CurrentUser = account;
        Touch();
        return OperationResult.Success(member.Id);
    }

    public OperationResult Login(string phone, string password)
    {
        var p = Validation.NormalizePhone(phone);
        var pwd = password ?? "";

        if (p != null && IsLocked(p, out var remaining))
        {
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            return OperationResult.Fail($"Trop de tentatives. Réessayez dans {minutes} min.");
        }

        var account = p == null ? null : Data.Accounts.FirstOrDefault(a => a.Phone == p);
        bool ok;
        if (account == null)
        {
            Security.BurnTime(pwd);        // même durée de réponse qu'un vrai compte : on ne révèle pas quels numéros existent
            ok = false;
        }
        else ok = Security.Verify(pwd, account.PasswordHash, account.Salt);

        if (!ok)
        {
            if (p != null) RegisterLoginFailure(p);
            return OperationResult.Fail("Numéro ou mot de passe incorrect.");
        }

        _loginAttempts.Remove(p!);
        if (Security.NeedsRehash(account!.PasswordHash))        // ancien hash (50 000 itérations) : on le renforce
        {
            var (hash, salt) = Security.HashPassword(pwd);
            account.PasswordHash = hash;
            account.Salt = salt;
            Touch();
        }
        CurrentUser = account;
        return OperationResult.Success(account.MemberId);
    }

    private bool IsLocked(string phone, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_loginAttempts.TryGetValue(phone, out var state)) return false;
        var left = state.LockedUntilUtc - _utcNow();
        if (left <= TimeSpan.Zero) return false;
        remaining = left;
        return true;
    }

    private void RegisterLoginFailure(string phone)
    {
        _loginAttempts.TryGetValue(phone, out var state);
        var now = _utcNow();
        // Un blocage déjà expiré repart de zéro.
        int failures = (state.LockedUntilUtc != default && state.LockedUntilUtc <= now) ? 1 : state.Failures + 1;
        var lockedUntil = failures >= MaxLoginAttempts ? now + LoginLockDuration : default;
        _loginAttempts[phone] = (failures, lockedUntil);
    }

    public void Logout() => CurrentUser = null;

    /// <summary>Remplace toutes les données (réinitialisation de la démonstration).</summary>
    public void ReplaceData(NjangiData data)
    {
        Data = data;
        CurrentUser = null;
        Touch();
    }

    private OperationResult? RequireManager() =>
        CanManage ? null : OperationResult.Fail("Action réservée au président ou au trésorier.");

    // ----------------------------------------------------------------- Membres

    /// <summary>Crée le groupe provisoire et son premier membre, qui en est le président.</summary>
    private Member CreateFounder(string name, string phone)
    {
        int year = Today.Year;
        Data.Group = new Group
        {
            Name = CoreTexts.DefaultGroupName,
            CycleYear = year,
            CycleStart = new DateTime(year, 1, 1),
            CycleEnd = new DateTime(year, 12, 31)
        };
        var founder = new Member { Id = NextId(Data.Members, m => m.Id), FullName = name, Phone = phone, Role = Role.President };
        Data.Members.Add(founder);
        return founder;
    }

    private Member CreateMember(string name, string phone)
    {
        var member = new Member { Id = NextId(Data.Members, m => m.Id), FullName = name, Phone = phone, Role = Role.Membre };
        Data.Members.Add(member);
        foreach (var s in Data.Sessions.Where(s => s.Date.Date >= Today))
        {
            Data.Contributions.Add(new Contribution
            {
                Id = NextId(Data.Contributions, c => c.Id),
                SessionId = s.Id,
                MemberId = member.Id,
                Amount = Data.Group.ContributionAmount
            });
        }
        return member;
    }

    public OperationResult AddMember(string fullName, string phone) => AddMember(fullName, phone, Role.Membre);

    public OperationResult AddMember(string fullName, string phone, Role role)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var errors = new List<string>();
        if (Validation.NameError(fullName) is { } e1) errors.Add(e1);
        if (Validation.PhoneError(phone) is { } e2) errors.Add(e2);
        if (!Enum.IsDefined(role) || role == Role.President) errors.Add("Le rôle choisi n'est pas autorisé pour un nouveau membre.");
        if (role != Role.Membre && !IsPresident) errors.Add("Seul le président peut attribuer un rôle de responsabilité.");
        if (errors.Count > 0) return OperationResult.Fail(errors.ToArray());
        var p = Validation.NormalizePhone(phone)!;
        if (Data.Members.Any(m => m.Phone == p)) return OperationResult.Fail("Ce numéro est déjà dans le groupe.");
        var m = CreateMember(Validation.CleanName(fullName), p);
        m.Role = role;
        Touch();
        return OperationResult.Success(m.Id);
    }

    /// <summary>Configure les paramètres du groupe. Les prêts et cotisations déjà créés ne sont jamais recalculés.</summary>
    public OperationResult ConfigureGroup(string name, DateTime cycleStart, DateTime cycleEnd,
        long contributionAmount, decimal monthlyRate, decimal loanFundShare,
        long lateFine, int meetingDay, string meetingTime, string host)
    {
        if (!IsPresident) return OperationResult.Fail("Action réservée au président.");
        var errors = new List<string>();
        if (Validation.NameError(name) is { } nameError) errors.Add(nameError);
        if (cycleStart.Date > cycleEnd.Date) errors.Add("La fin du cycle doit être postérieure au début.");
        if (cycleStart.Year < 2000 || cycleEnd.Year > 2100) errors.Add("Les dates du cycle doivent être comprises entre 2000 et 2100.");
        if (Validation.AmountError(contributionAmount) is { } amountError) errors.Add("Cotisation : " + amountError);
        if (Validation.RateError(monthlyRate) is { } rateError) errors.Add(rateError);
        if (loanFundShare < 0m || loanFundShare > 1m) errors.Add("La part de la caisse doit être comprise entre 0 % et 100 %.");
        if (Validation.AmountError(lateFine) is { } fineError) errors.Add("Amende : " + fineError);
        if (meetingDay < 1 || meetingDay > 31) errors.Add("Le jour de séance doit être compris entre 1 et 31.");
        if (string.IsNullOrWhiteSpace(meetingTime) || meetingTime.Length > 20) errors.Add("Saisissez une heure valide.");
        if (Validation.NameError(host) is { } hostError) errors.Add("Hôte : " + hostError);
        if (errors.Count > 0) return OperationResult.Fail(errors.ToArray());

        Data.Group.Name = Validation.CleanName(name);
        Data.Group.CycleStart = cycleStart.Date;
        Data.Group.CycleEnd = cycleEnd.Date;
        Data.Group.CycleYear = cycleStart.Year;
        Data.Group.ContributionAmount = contributionAmount;
        Data.Group.MonthlyRate = monthlyRate;
        Data.Group.LoanFundShare = loanFundShare;
        Data.Group.LateContributionFine = lateFine;
        Data.Group.LateRepaymentFine = lateFine;
        Data.Group.InvitationCode = string.Empty;
        if (Data.Sessions.Count == 0)
        {
            var calendar = GenerateMonthlySchedule(meetingDay, meetingTime.Trim(), Validation.CleanName(host));
            if (!calendar.Ok) return calendar;
        }
        Touch();
        return OperationResult.Success();
    }

    /// <summary>Génère une séance par mois, en bornant le jour au dernier jour réel du mois et à la fin du cycle.</summary>
    public OperationResult GenerateMonthlySchedule(int day, string timeText, string host)
    {
        if (day < 1 || day > 31) return OperationResult.Fail("Le jour de séance doit être compris entre 1 et 31.");
        if (Data.Group.CycleStart.Date > Data.Group.CycleEnd.Date) return OperationResult.Fail("Les dates du cycle sont invalides.");
        if (string.IsNullOrWhiteSpace(timeText) || timeText.Length > 20) return OperationResult.Fail("Saisissez une heure valide.");
        if (Validation.NameError(host) is not null) return OperationResult.Fail("Saisissez le nom de l'hôte.");

        // Ne pas dupliquer les séances déjà existantes : la génération est réservée à une configuration initiale.
        if (Data.Sessions.Count > 0) return OperationResult.Fail("Le calendrier existe déjà. Modifiez les séances existantes plutôt que de le régénérer.");
        var generated = new List<MeetingSession>();
        var cursor = new DateTime(Data.Group.CycleStart.Year, Data.Group.CycleStart.Month, 1);
        while (cursor <= Data.Group.CycleEnd.Date)
        {
            var actualDay = Math.Min(day, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var date = new DateTime(cursor.Year, cursor.Month, actualDay);
            if (date >= Data.Group.CycleStart.Date && date <= Data.Group.CycleEnd.Date)
                generated.Add(new MeetingSession { Id = generated.Count + 1, Date = date, Host = host.Trim(), TimeText = timeText.Trim() });
            cursor = cursor.AddMonths(1);
        }
        if (generated.Count == 0) return OperationResult.Fail("Aucune séance ne se trouve dans les dates du cycle.");
        Data.Sessions.AddRange(generated);
        foreach (var session in generated)
            foreach (var member in Data.Members)
                Data.Contributions.Add(new Contribution { Id = NextId(Data.Contributions, c => c.Id), SessionId = session.Id, MemberId = member.Id, Amount = Data.Group.ContributionAmount });
        return OperationResult.Success(generated.Count);
    }

    /// <summary>Le président attribue les rôles de trésorier ou secrétaire à un membre existant.</summary>
    public OperationResult AssignRole(int memberId, Role role)
    {
        if (!IsPresident) return OperationResult.Fail("Action réservée au président.");
        if (role is not (Role.Membre or Role.Tresorier or Role.Secretaire)) return OperationResult.Fail("Rôle non autorisé.");
        var member = Member(memberId);
        if (member == null || member.Role == Role.President) return OperationResult.Fail("Membre introuvable ou rôle non modifiable.");
        member.Role = role;
        Touch();
        return OperationResult.Success(memberId);
    }

    /// <summary>Le président crée un accès pour un membre avec un mot de passe temporaire.</summary>
    public OperationResult CreateMemberAccount(int memberId, string temporaryPassword)
    {
        if (!IsPresident) return OperationResult.Fail("Action réservée au président.");
        var member = Member(memberId);
        if (member == null) return OperationResult.Fail("Membre introuvable.");
        if (Data.Accounts.Any(a => a.MemberId == memberId || a.Phone == member.Phone)) return OperationResult.Fail("Ce membre possède déjà un compte.");
        if (Validation.PasswordError(temporaryPassword) is { } error) return OperationResult.Fail(error);
        var (hash, salt) = Security.HashPassword(temporaryPassword);
        Data.Accounts.Add(new UserAccount { Phone = member.Phone, FullName = member.FullName, PasswordHash = hash, Salt = salt, MemberId = member.Id, MustChangePassword = true });
        Touch();
        return OperationResult.Success(memberId);
    }

    public Member? Member(int id) => Data.Members.FirstOrDefault(m => m.Id == id);

    public Standing StandingOf(int memberId)
    {
        var loanIds = Data.Loans.Where(l => l.MemberId == memberId && l.Status == LoanStatus.Approved).Select(l => l.Id).ToHashSet();
        if (Data.Installments.Any(i => loanIds.Contains(i.LoanId) && StatusOf(i) == InstallmentStatus.Late))
            return Standing.LoanLate;
        if (Data.Contributions.Any(c => c.MemberId == memberId && StatusOf(c) == ContributionStatus.Late))
            return Standing.ContributionLate;
        return Standing.UpToDate;
    }

    // ------------------------------------------------------------- Cotisations

    public MeetingSession? NextSession() =>
        Data.Sessions.Where(s => s.Date.Date >= Today).OrderBy(s => s.Date).FirstOrDefault();

    public List<Contribution> ContributionsOf(int sessionId) =>
        Data.Contributions.Where(c => c.SessionId == sessionId).OrderBy(c => c.MemberId).ToList();

    public ContributionStatus StatusOf(Contribution c)
    {
        if (c.PaidOn != null) return ContributionStatus.Paid;
        var session = Data.Sessions.FirstOrDefault(s => s.Id == c.SessionId);
        if (session == null) return ContributionStatus.Upcoming;   // donnée orpheline : pas d'exception, pas de fausse amende
        return session.Date.Date < Today ? ContributionStatus.Late : ContributionStatus.Upcoming;
    }

    public OperationResult RecordContributionPayment(int contributionId)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var c = Data.Contributions.FirstOrDefault(x => x.Id == contributionId);
        if (c == null) return OperationResult.Fail("Cotisation introuvable.");
        if (c.PaidOn != null) return OperationResult.Fail("Cette cotisation est déjà payée.");
        c.PaidOn = Today;
        RefreshFines();
        Touch();
        return OperationResult.Success(c.Id);
    }

    // ------------------------------------------------------------------- Prêts

    public long CashAvailable()
    {
        long contributionsPaid = Data.Contributions.Where(c => c.PaidOn != null).Sum(c => c.Amount);
        long fund = FundPart(contributionsPaid);
        long installmentsPaid = Data.Installments.Where(i => i.PaidOn != null).Sum(i => i.Amount);
        long lent = Data.Loans.Where(l => l.Status is LoanStatus.Approved or LoanStatus.Closed).Sum(l => l.Principal);
        return fund + PaidFinesTotal() + installmentsPaid - lent;
    }

    /// <summary>Vrai si la caisse permet au moins un prêt du montant minimum.</summary>
    public bool HasLoanableCash() => CashAvailable() >= Limits.MinLoan;

    private long FundPart(long contributions) =>
        Money.Share(contributions, Data.Group.LoanFundShare);

    public long InterestReceived() => Data.Installments.Where(i => i.PaidOn != null).Sum(i => i.InterestPart);

    public List<Loan> ActiveLoans() =>
        Data.Loans.Where(l => l.Status == LoanStatus.Approved).OrderBy(l => l.ApprovedOn).ToList();

    public List<Installment> InstallmentsOf(int loanId) =>
        Data.Installments.Where(i => i.LoanId == loanId).OrderBy(i => i.Number).ToList();

    public InstallmentStatus StatusOf(Installment i)
    {
        if (i.PaidOn != null) return InstallmentStatus.Paid;
        return i.DueDate.Date < Today ? InstallmentStatus.Late : InstallmentStatus.Upcoming;
    }

    public int LateDays(Installment i) =>
        StatusOf(i) == InstallmentStatus.Late ? LoanCalculator.OverdueDays(i.DueDate, Today) : 0;

    public long RepaidOf(int loanId) => Data.Installments.Where(i => i.LoanId == loanId && i.PaidOn != null).Sum(i => i.Amount);

    public long TotalDueOf(int loanId) => Data.Installments.Where(i => i.LoanId == loanId).Sum(i => i.Amount);

    /// <summary>Devis de prêt calculé par le Core avec le taux actuel du groupe.</summary>
    public LoanQuote? QuoteLoan(long principal, int months) =>
        LoanCalculator.TryQuote(principal, months, Data.Group.MonthlyRate, out var quote) ? quote : null;

    /// <summary>Date de première échéance prévue selon la même règle que l'approbation.</summary>
    public DateTime FirstLoanDueDate() => Today.AddMonths(1);

    /// <summary>Règles d'une demande de prêt. Retourne la liste des problèmes (vide si tout est bon).</summary>
    public List<string> ValidateLoanRequest(int memberId, long principal, int months)
    {
        var errors = new List<string>();
        if (Member(memberId) == null) { errors.Add("Choisissez un membre."); return errors; }
        if (principal < Limits.MinLoan) errors.Add($"Le montant minimum est de {Formatting.Fcfa(Limits.MinLoan)}.");
        else if (principal > Limits.MaxAmount) errors.Add($"Le montant maximum est de {Formatting.Fcfa(Limits.MaxAmount)}.");
        if (Validation.MonthsError(months) is { } monthsError) errors.Add(monthsError);
        if (Validation.RateError(Data.Group.MonthlyRate) is { } rateError) errors.Add("Taux du groupe invalide : " + rateError);
        var cash = CashAvailable();
        if (principal > cash) errors.Add($"Le montant dépasse la caisse disponible ({Formatting.Fcfa(cash)}).");
        if (Data.Loans.Any(l => l.MemberId == memberId && l.Status is LoanStatus.Pending or LoanStatus.Approved))
            errors.Add("Ce membre a déjà un prêt en cours ou en attente.");
        if (StandingOf(memberId) != Standing.UpToDate)
            errors.Add("Ce membre a un retard à régulariser avant un nouveau prêt.");
        return errors;
    }

    public OperationResult RequestLoan(int memberId, long principal, int months)
    {
        if (CurrentMember == null) return OperationResult.Fail("Connectez-vous d'abord.");
        if (!CanManage && CurrentMember.Id != memberId)
            return OperationResult.Fail("Vous ne pouvez demander un prêt que pour vous-même.");
        var errors = ValidateLoanRequest(memberId, principal, months);
        if (errors.Count > 0) return OperationResult.Fail(errors.ToArray());

        var loan = new Loan
        {
            Id = NextId(Data.Loans, l => l.Id),
            MemberId = memberId,
            Principal = principal,
            DurationMonths = months,
            MonthlyRate = Data.Group.MonthlyRate,
            RequestedOn = Today,
            Status = LoanStatus.Pending
        };
        Data.Loans.Add(loan);
        Touch();
        return OperationResult.Success(loan.Id);
    }

    public OperationResult ApproveLoan(int loanId)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var loan = Data.Loans.FirstOrDefault(l => l.Id == loanId);
        if (loan == null || loan.Status != LoanStatus.Pending) return OperationResult.Fail("Cette demande ne peut plus être approuvée.");
        var cash = CashAvailable();
        if (loan.Principal > cash) return OperationResult.Fail($"La caisse ne contient que {Formatting.Fcfa(cash)}.");

        // Le calcul passe en premier : s'il échoue, le prêt reste « en attente », intact.
        var firstDue = Today.AddMonths(1);
        if (!LoanCalculator.TryBuildSchedule(loan, firstDue, out var schedule, out var scheduleError))
            return OperationResult.Fail(scheduleError!);

        loan.Status = LoanStatus.Approved;
        loan.ApprovedOn = Today;
        loan.FirstDueDate = firstDue;
        int nextId = NextId(Data.Installments, i => i.Id);
        foreach (var inst in schedule)
        {
            inst.Id = nextId++;
            Data.Installments.Add(inst);
        }
        Touch();
        return OperationResult.Success(loan.Id);
    }

    public OperationResult RejectLoan(int loanId)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var loan = Data.Loans.FirstOrDefault(l => l.Id == loanId);
        if (loan == null || loan.Status != LoanStatus.Pending) return OperationResult.Fail("Cette demande ne peut plus être refusée.");
        loan.Status = LoanStatus.Rejected;
        Touch();
        return OperationResult.Success(loan.Id);
    }

    /// <summary>Enregistre le paiement de la plus ancienne mensualité non payée.</summary>
    public OperationResult RecordNextInstallmentPayment(int loanId)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var loan = Data.Loans.FirstOrDefault(l => l.Id == loanId);
        if (loan == null || loan.Status != LoanStatus.Approved) return OperationResult.Fail("Ce prêt n'est pas en cours.");
        var next = InstallmentsOf(loanId).FirstOrDefault(i => i.PaidOn == null);
        if (next == null) return OperationResult.Fail("Toutes les mensualités sont déjà payées.");
        next.PaidOn = Today;
        if (InstallmentsOf(loanId).All(i => i.PaidOn != null)) loan.Status = LoanStatus.Closed;
        RefreshFines();
        Touch();
        return OperationResult.Success(next.Id);
    }

    // ----------------------------------------------------------------- Amendes

    /// <summary>
    /// Ajoute les amendes manquantes : une par cotisation ou mensualité en retard.
    /// Peut être appelée autant de fois qu'on veut : une même cause ne donne jamais deux amendes.
    /// </summary>
    public int RefreshFines()
    {
        int added = 0;

        foreach (var c in Data.Contributions)
        {
            var session = Data.Sessions.FirstOrDefault(s => s.Id == c.SessionId);
            if (session == null) continue;
            bool late = (c.PaidOn?.Date ?? Today) > session.Date.Date;
            if (!late || FineExists(FineReason.Contribution, c.Id)) continue;
            AddFine(c.MemberId, FineReason.Contribution, c.Id, Data.Group.LateContributionFine);
            added++;
        }

        foreach (var i in Data.Installments)
        {
            var loan = Data.Loans.FirstOrDefault(l => l.Id == i.LoanId);
            if (loan == null || loan.Status is LoanStatus.Pending or LoanStatus.Rejected) continue;
            bool late = (i.PaidOn?.Date ?? Today) > i.DueDate.Date;
            if (!late || FineExists(FineReason.Installment, i.Id)) continue;
            AddFine(loan.MemberId, FineReason.Installment, i.Id, Data.Group.LateRepaymentFine);
            added++;
        }

        if (added > 0) Touch();
        return added;
    }

    private bool FineExists(FineReason reason, int sourceId) =>
        Data.Fines.Any(f => f.Reason == reason && f.SourceId == sourceId);

    private void AddFine(int memberId, FineReason reason, int sourceId, long amount) =>
        Data.Fines.Add(new Fine
        {
            Id = NextId(Data.Fines, f => f.Id),
            MemberId = memberId,
            Reason = reason,
            SourceId = sourceId,
            Amount = amount,
            IssuedOn = Today
        });

    public List<Fine> PendingFines() =>
        Data.Fines.Where(f => f.PaidOn == null && !f.Cancelled).OrderBy(f => f.IssuedOn).ThenBy(f => f.Id).ToList();

    public long PendingFinesTotal() => PendingFines().Sum(f => f.Amount);

    public long PaidFinesTotal() => Data.Fines.Where(f => f.PaidOn != null && !f.Cancelled).Sum(f => f.Amount);

    public string DescribeFine(Fine f)
    {
        if (f.Reason == FineReason.Contribution)
        {
            var c = Data.Contributions.FirstOrDefault(x => x.Id == f.SourceId);
            var s = c == null ? null : Data.Sessions.FirstOrDefault(x => x.Id == c.SessionId);
            return s == null ? "Cotisation en retard" : $"Cotisation du {Formatting.DayMonth(s.Date)}";
        }
        var i = Data.Installments.FirstOrDefault(x => x.Id == f.SourceId);
        return i == null ? "Remboursement en retard" : $"Mensualité du {Formatting.DayMonth(i.DueDate)}";
    }

    public OperationResult PayFine(int fineId)
    {
        var denied = RequireManager();
        if (denied != null) return denied;
        var f = Data.Fines.FirstOrDefault(x => x.Id == fineId);
        if (f == null || f.PaidOn != null || f.Cancelled) return OperationResult.Fail("Cette amende n'est plus à encaisser.");
        f.PaidOn = Today;
        Touch();
        return OperationResult.Success(f.Id);
    }

    public OperationResult CancelFine(int fineId)
    {
        if (!IsPresident) return OperationResult.Fail("Seul le président (ou la présidente) peut annuler une amende.");
        var f = Data.Fines.FirstOrDefault(x => x.Id == fineId);
        if (f == null || f.PaidOn != null || f.Cancelled) return OperationResult.Fail("Cette amende n'est plus à encaisser.");
        f.Cancelled = true;
        Touch();
        return OperationResult.Success(f.Id);
    }

    // --------------------------------------------------- Tableau de bord, bilan

    public DashboardSummary GetDashboard()
    {
        var next = NextSession();
        var pending = PendingFines();
        return new DashboardSummary(
            string.IsNullOrWhiteSpace(Data.Group.Name) ? CoreTexts.NoGroupName : Data.Group.Name,
            CycleLabel(),
            CashAvailable(),
            ActiveLoans().Count,
            InterestReceived(),
            pending.Count,
            pending.Sum(f => f.Amount),
            next,
            next == null ? 0 : Data.Contributions.Count(c => c.SessionId == next.Id && c.PaidOn != null),
            Data.Members.Count);
    }

    /// <summary>« Cycle 2026 · mois 10 sur 12 », ou un libellé neutre si le groupe n'a pas de cycle défini.</summary>
    private string CycleLabel()
    {
        var g = Data.Group;
        if (g.CycleYear <= 0 || g.CycleStart.Year < 2000) return CoreTexts.NoCycleLabel;
        int monthNumber = Math.Clamp(
            (Today.Year - g.CycleStart.Year) * 12 + Today.Month - g.CycleStart.Month + 1, 1, 12);
        return $"Cycle {g.CycleYear} · mois {monthNumber} sur 12";
    }

    /// <summary>
    /// Bilan du cycle : cotisations versées à la caisse + intérêts + amendes encaissées.
    /// Les gains (intérêts + amendes) sont partagés à parts égales entre les membres.
    /// Avec includePlanned = true, on projette : toutes les cotisations prévues et tous les intérêts des prêts.
    /// </summary>
    public CycleReport BuildReport(bool includePlanned)
    {
        var members = Data.Members.OrderBy(m => m.Id).ToList();
        long interest = includePlanned
            ? Data.Installments.Where(i => Data.Loans.Any(l => l.Id == i.LoanId && l.Status is LoanStatus.Approved or LoanStatus.Closed)).Sum(i => i.InterestPart)
            : InterestReceived();
        long fines = PaidFinesTotal();
        long gains = interest + fines;

        int n = Math.Max(members.Count, 1);
        long each = gains / n;
        long remainder = gains % n;

        var shares = new List<MemberShare>();
        long contributionsTotal = 0;
        for (int idx = 0; idx < members.Count; idx++)
        {
            var m = members[idx];
            long paid = Data.Contributions
                .Where(c => c.MemberId == m.Id && (includePlanned || c.PaidOn != null))
                .Sum(c => c.Amount);
            long contributed = FundPart(paid);
            long gain = each + (idx < remainder ? 1 : 0);
            contributionsTotal += contributed;
            shares.Add(new MemberShare(m, contributed, gain, contributed + gain));
        }
        return new CycleReport(contributionsTotal, interest, fines, contributionsTotal + gains, shares, includePlanned);
    }

    public string BuildReportText(bool includePlanned)
    {
        var r = BuildReport(includePlanned);
        var lines = new List<string>
        {
            $"Bilan {Data.Group.Name} · cycle {Data.Group.CycleYear}" + (r.IsProjection ? " (projection)" : ""),
            $"Cotisations versées à la caisse : {Formatting.Fcfa(r.Contributions)}",
            $"Intérêts : {Formatting.Fcfa(r.Interest)}",
            $"Amendes : {Formatting.Fcfa(r.Fines)}",
            $"Total à partager : {Formatting.Fcfa(r.TotalToShare)}",
            ""
        };
        lines.AddRange(r.Shares.Select(s => $"{s.Member.FullName} : {Formatting.Fcfa(s.Total)}"));
        return string.Join("\n", lines);
    }
}
