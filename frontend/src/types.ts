export interface TaxpayerProfile {
  nameAr: string
  nameEn?: string | null
  tin: string
  crNumber: string
  legalForm: string
  commercialActivity: string
}

export interface DisputedItem {
  itemId: string
  descriptionAr: string
  claimedAmountQar: number
  taxpayerDefense: string
  taxpayerLegalReference: string
  attachmentsPresent: boolean
}

export interface DhareebaRecord {
  assessmentNoticeRef: string
  assessmentNoticeDate: string
  statutoryObjectionDeadline: string
  administrativeObjectionFiled: boolean
  administrativeObjectionDate?: string | null
  originalAssessedTaxDiffQar: number
  originalDelayPenaltiesQar: number
  totalOriginalClaimQar: number
}

export interface DisputeDossier {
  dossierId: string
  committeeRecordNumber: string
  committeeFilingDate: string
  disputedFiscalYear: string
  taxpayer: TaxpayerProfile
  dhareebaRecord: DhareebaRecord
  disputedItems: DisputedItem[]
}

export interface StatutoryBasis { source: string; article: string; ruleSummary: string }
export interface JurisprudenceDoctrine { court: string; principleAr: string }

export interface ProceduralVerdict {
  isAdmissibleFormally: boolean
  rulingRecommendation: string
  primaryPleaType: string
  assessmentNoticeDate: string
  statutoryObjectionDeadline: string
  actualObjectionDate?: string | null
  grievanceCommitteeFilingDate: string
  daysElapsedSinceNotice: number
  proceduralViolationCode?: string | null
  governingLegalBasis: StatutoryBasis[]
  jurisprudenceDoctrine: JurisprudenceDoctrine
  formulatedDefenseClauseAr: string
}

export type DeterminationType = 'RejectedFully' | 'AcceptedPartially' | 'AcceptedFully'

export interface ItemEvaluation {
  itemId: string
  lineName: string
  claimedAmountQar: number
  gtaDetermination: DeterminationType
  admittedDeductionQar: number
  statutoryReference: string
  legalReasoningAr: string
  evidenceStatus: string
}

export interface SubstantiveAuditSummary {
  totalDisputedClaimedQar: number
  totalConcessionsAdmittedQar: number
  totalDisallowedConfirmedQar: number
  defensePosture: string
  lineItemsEvaluation: ItemEvaluation[]
}

export interface LedgerEntry {
  ledgerEntryName: string
  originalAssessmentQar: number
  settlementProposalQar: number
  varianceQar: number
  legalBasis: string
}

export interface FinancialRecalculationResult {
  currency: string
  computationMode: string
  corporateTaxRate: number
  revisedTaxDiffQar: number
  revisedDelayPenaltiesQar: number
  isPenaltyCapped: boolean
  revisedTotalDueQar: number
  taxReliefQar: number
  penaltyReliefQar: number
  finalTreasuryReceivableQar: number
  ledgerComparisonMatrix: LedgerEntry[]
}

export interface CaseAnalysis {
  dossierId: string
  procedural: ProceduralVerdict
  substantive: SubstantiveAuditSummary
  financial: FinancialRecalculationResult
}

export interface MemoMetadata {
  state: string
  authority: string
  department: string
  referenceNumber: string
  filingDate: string
  addressee: string
}

export interface AssembledMemo {
  documentType: string
  metadata: MemoMetadata
  sections: Record<string, string>
  finalRequests: string[]
  narrativeSource: string
  generatedAtUtc: string
}

export interface DemoCaseInfo { key: string; titleAr: string; scenario: string; descriptionAr: string }

export interface DossierSummary {
  dossierId: string
  committeeRecordNumber: string
  taxpayerNameAr: string
  disputedFiscalYear: string
  committeeFilingDate: string
  source: string
}

export interface Health { status: string; aiExtractionEnabled: boolean; aiNarrativeEnabled: boolean; legalSearchEnabled: boolean; legalChatEnabled: boolean }

export interface MemoSectionState { key: string; title: string; text: string; done: boolean }
